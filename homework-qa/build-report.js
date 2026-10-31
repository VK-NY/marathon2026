// Builds US11-guest-menu-qa.xlsx from qa-data.js without third-party packages.
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const { requirements, testCases, defects } = require('./qa-data');

const escapeXml = (value) =>
  String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

// Style 0 is the plain default: Excel ignores wrapping set on it.
const STYLE = { note: 0, body: 1, header: 2, title: 3 };

function column(index) {
  return String.fromCharCode(65 + index);
}

// No merged cells and no fixed heights: Excel and Google Sheets fit the rows to the wrapped text themselves.
function sheetXml(rows, widths) {
  const cols = widths
    .map((width, index) => `<col min="${index + 1}" max="${index + 1}" width="${width}" customWidth="1"/>`)
    .join('');
  const body = rows
    .map((row, rowIndex) => {
      if (row.cells.length === 0) return `<row r="${rowIndex + 1}"/>`;
      const style = row.style ?? STYLE.body;
      const count = style === STYLE.title || style === STYLE.note ? 1 : widths.length;
      const cells = widths
        .slice(0, count)
        .map((_, cellIndex) => {
          const value = row.cells[cellIndex];
          const ref = `${column(cellIndex)}${rowIndex + 1}`;
          if (value === null || value === undefined || value === '') return `<c r="${ref}" s="${style}"/>`;
          return `<c r="${ref}" s="${style}" t="inlineStr"><is><t xml:space="preserve">${escapeXml(value)}</t></is></c>`;
        })
        .join('');
      return `<row r="${rowIndex + 1}">${cells}</row>`;
    })
    .join('');
  return (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
    `<cols>${cols}</cols><sheetData>${body}</sheetData></worksheet>`
  );
}

const requirementRows = [
  { style: STYLE.title, cells: ['Requirements analysis: US11 Guest Viewing Reservation Menu'] },
  { style: STYLE.note, cells: ['Sources: User Story PDF, Figma (Guest Flow), reference screenshot and a live session on a test deployment.'] },
  { style: STYLE.header, cells: ['ID', 'Where', 'Shortcoming', 'Why it matters', 'Suggested fix'] },
  ...requirements.map((item) => ({ cells: [item.id, item.where, item.issue, item.impact, item.fix] })),
];

const testCaseRows = [{ style: STYLE.title, cells: ['Test cases: US11 Guest Viewing Reservation Menu'] }];
for (const item of testCases) {
  testCaseRows.push({ style: STYLE.header, cells: [`ID: ${item.id}`, `Title: ${item.title}`, '', `Status: ${item.status}`] });
  testCaseRows.push({ cells: ['', `Pre-conditions:\n${item.pre}`] });
  testCaseRows.push({
    cells: [
      '',
      `Steps:\n${item.steps.map((step, index) => `${index + 1}. ${step}`).join('\n')}`,
      `Expected result:\n${item.expected.map((step, index) => `${index + 1}. ${step}`).join('\n')}`,
      item.note ? `Note: ${item.note}` : '',
    ],
  });
  testCaseRows.push({ cells: [] });
}

const defectRows = [{ style: STYLE.title, cells: ['Defect reports: US11 Guest Viewing Reservation Menu'] }];
for (const item of defects) {
  defectRows.push({
    style: STYLE.header,
    cells: [`ID: ${item.id}`, `Summary: ${item.summary}`, `Env: ${item.env}\nPriority: ${item.priority}\nSeverity: ${item.severity}`],
  });
  defectRows.push({ cells: ['', `Pre-conditions:\n${item.pre}`] });
  defectRows.push({
    cells: [
      '',
      `Steps:\n${item.steps.map((step, index) => `${index + 1}. ${step}`).join('\n')}\n\n` +
        `Actual result: ${item.actual}\n\nExpected result: ${item.expected}`,
    ],
  });
  defectRows.push({ cells: ['', `Attachments: ${item.evidence}`] });
  defectRows.push({ cells: [] });
}

const sheets = [
  { name: 'Requirements analysis', xml: sheetXml(requirementRows, [8, 22, 60, 50, 50]) },
  { name: 'Test cases', xml: sheetXml(testCaseRows, [10, 70, 70, 34]) },
  { name: 'Defect reports', xml: sheetXml(defectRows, [10, 110, 44]) },
];

const files = {
  '[Content_Types].xml':
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
    '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
    '<Default Extension="xml" ContentType="application/xml"/>' +
    '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
    '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
    sheets
      .map(
        (_, index) =>
          `<Override PartName="/xl/worksheets/sheet${index + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`,
      )
      .join('') +
    '</Types>',
  '_rels/.rels':
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
    '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
    '</Relationships>',
  'xl/workbook.xml':
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" ' +
    'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>' +
    sheets.map((sheet, index) => `<sheet name="${sheet.name}" sheetId="${index + 1}" r:id="rId${index + 1}"/>`).join('') +
    '</sheets></workbook>',
  'xl/_rels/workbook.xml.rels':
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
    sheets
      .map(
        (_, index) =>
          `<Relationship Id="rId${index + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${index + 1}.xml"/>`,
      )
      .join('') +
    `<Relationship Id="rId${sheets.length + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>` +
    '</Relationships>',
  'xl/styles.xml':
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
    '<fonts count="3"><font><sz val="10"/><name val="Arial"/></font>' +
    '<font><b/><sz val="10"/><name val="Arial"/></font><font><b/><sz val="14"/><name val="Arial"/></font></fonts>' +
    '<fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>' +
    '<fill><patternFill patternType="solid"><fgColor rgb="FFD9E5D9"/></patternFill></fill></fills>' +
    '<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border>' +
    '<border><left style="thin"><color rgb="FFBFBFBF"/></left><right style="thin"><color rgb="FFBFBFBF"/></right>' +
    '<top style="thin"><color rgb="FFBFBFBF"/></top><bottom style="thin"><color rgb="FFBFBFBF"/></bottom><diagonal/></border></borders>' +
    '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
    '<cellXfs count="4">' +
    '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>' +
    '<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>' +
    '<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>' +
    '<xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>' +
    '</cellXfs></styleSheet>',
};
sheets.forEach((sheet, index) => {
  files[`xl/worksheets/sheet${index + 1}.xml`] = sheet.xml;
});

function zip(entries) {
  const locals = [];
  const centrals = [];
  let offset = 0;
  for (const [name, content] of Object.entries(entries)) {
    const nameBytes = Buffer.from(name);
    const data = Buffer.from(content);
    const deflated = zlib.deflateRawSync(data);
    const crc = zlib.crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(8, 8);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(deflated.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(8, 10);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(deflated.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBytes.length, 28);
    central.writeUInt32LE(offset, 42);

    locals.push(local, nameBytes, deflated);
    centrals.push(central, nameBytes);
    offset += local.length + nameBytes.length + deflated.length;
  }
  const centralDirectory = Buffer.concat(centrals);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(Object.keys(entries).length, 8);
  end.writeUInt16LE(Object.keys(entries).length, 10);
  end.writeUInt32LE(centralDirectory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, centralDirectory, end]);
}

const output = path.join(__dirname, 'US11-guest-menu-qa.xlsx');
fs.writeFileSync(output, zip(files));
console.log(`saved ${output}: ${requirements.length} requirement issues, ${testCases.length} test cases, ${defects.length} defects`);
