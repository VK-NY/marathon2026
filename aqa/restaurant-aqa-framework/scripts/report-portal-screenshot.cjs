// Signs in to the job's short-lived ReportPortal and saves a picture of the launches. Used by aqa-report-portal.yml.
const { chromium } = require('playwright');

(async () => {
  const [output] = process.argv.slice(2);
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });

  await page.goto(`${process.env.RP_URL}/ui/`);
  await page.locator('input[name="login"]').fill('superadmin');
  await page.locator('input[name="password"]').fill(process.env.RP_ADMIN_PASSWORD);
  await page.getByRole('button', { name: /login/i }).click();
  await page.waitForURL(/#/);

  await page.goto(`${process.env.RP_URL}/ui/#${process.env.RP_PROJECT}/launches/all`);
  await page.getByText('Group pre-order').first().waitFor({ timeout: 60000 });
  await page.screenshot({ path: output });
  await browser.close();
})().catch((error) => {
  console.error(error);
  process.exit(1);
});
