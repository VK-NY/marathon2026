# Restaurant AQA framework

**Chosen direction: Direction 1, group pre-order** (my own backend and frontend, integrated and deployed).
**Approach: BDD + Screenplay** (Reqnroll, TUnit, Playwright for .NET), the framework's own architecture.

## Homework solution: group pre-order

| What | Where |
|---|---|
| UI scenarios: what the owner sees, sending the pre-order, removing a guest, what the waiter sees | `Features/GroupPreOrderScreen.feature` |
| API scenarios: authorization and validation, data preparation | `Features/GroupPreOrderRules.feature` |
| Steps, tasks, questions, page objects, API client | `StepDefinitions/Group*`, `Screenplay/Tasks/*Group*`, `Screenplay/Questions/Group*`, `Ui/GroupOrderPage.cs`, `Api/GroupPreOrdersApi.cs` |

40 scenarios: 9 UI, 31 API, levels 1 to 3.

### Run a subset

```bash
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"   # the whole solution
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level2]"           # one level: level1, level2, level3
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=ui]"               # ui or api
```

Reports land in `TestResults/` (see "Run the tests"). In CI: Actions, AQA Tests, Run workflow; the reports and traces are the run's artifacts.

### Behaviour the homework leaves open

| Case | Chosen behaviour, tested as such |
|---|---|
| Confirming a group a second time | 400, "Group reservation was submitted." |
| Confirming or viewing as a non-Owner | 403 for a signed-in customer or waiter, 401 without sign-in, and 401 for a guest, whose session token is not a sign-in token |
| Removing the last participant | Allowed: `participants: []`, `total: 0` |
| Removing the Owner's own session | 400, the Owner stays in the group |
| Group without participants | `participants: []`, `total: 0` |
| Owner's dishes | They live in the Owner's order, which must exist before confirming (400 otherwise). They are listed on the Owner's card, and guests' confirmed dishes are added to that order on confirmation |
| Totals | API `total` is the sum over all participants. The screen's Grand Total counts confirmed carts only, as that is what goes to the restaurant |
| "No dishes in confirmed carts" | Cannot be reached: an empty cart cannot be confirmed and a confirmed cart cannot change, so the rule that keeps it so is tested instead |

### Standing test data

Scenarios create their own customer, table, reservation and guests, with unique names and times computed from now. These must exist once in the environment, in `.env` or as CI variables:

- `TEST_LOCATION_ID`: a location with one waiter, for the bulk of the scenarios.
- `WAITER_LOCATION_ID`: a location for the scenarios that end at the waiter, each of which hires a waiter of its own.
- In both: dishes "QA Salad" ($12.50), "QA Soup" ($8.00), "QA Pasta" ($15.00). They are not removed, as deleting a dish other reservations refer to would break those.
- `WAITER_USERNAME`, `WAITER_PASSWORD`: the standing waiter, also used by the framework's own scenarios.

**Cleanup.** Reservations are cancelled after each scenario, except in the 2 scenarios that end at the waiter: a cancelled reservation could send the next booking to an old waiter, so these keep theirs. Tables stay on purpose (see below). The backend has no route to delete a customer, a waiter or a guest session, so those stay too. Customers and waiters are recognisable by the `AQA-Sample-` first name and the `aqa.sample.` email; everything else is unique per run.

### Defects found

| Defect | Effect |
|---|---|
| `POST /waiters` needs no sign-in | Anyone can make any account a waiter. The waiter scenarios rely on it for now, see `Api/WaitersApi.cs` |
| Deleting a table with a reservation on it | `GET /reservations` then answers 500 for that waiter and the waiter's whole list is lost, which is why tests keep their tables |
| The waiter's list asks for feedback, name and order per reservation, all at once | With the account's Lambda limit of 10 concurrent runs, requests are throttled and answered without CORS headers. The card then shows "Pre-order: 0 dishes" and no button. The waiter task reloads the page for this reason |
| A reservation without an order gives a 404 on the waiter's list | The waiter sees "An unexpected error occurred" although nothing is wrong |
| The notice about unconfirmed carts counts the Owner | With one guest still choosing it says "2 participants" |

The backend runs on a 128 MB Lambda, so a cold start can pass the default 15 s. CI therefore sets `DEFAULT_TIMEOUT_MS=45000` and `MAX_PARALLEL_TESTS=2`; use the same in `.env`.

The 6 failures of a complete run are the framework's own `@known-defect` scenarios, not part of this solution.

### Report Portal (starred task)

Actions, **AQA Report Portal**, Run workflow. The job starts ReportPortal in containers on the GitHub runner, sends the group pre-order scenarios to it through the `ReportPortal.Reqnroll` agent, and keeps the launch data and a screenshot in the `report-portal` artifact. ReportPortal exists only during the job, so nothing runs or costs money in between, and its password is generated per run. Everywhere else the agent is off (`ReportPortal.json`).

## About the framework

End-to-end tests for the Green & Tasty restaurant application: Reqnroll (Gherkin) + TUnit + Playwright for .NET, with the Screenplay pattern.

Guidance for extending the suite (writing Gherkin, steps, Screenplay, test data, working with an AI assistant) is in [CLAUDE.md](CLAUDE.md).

## Run the tests

```bash
cp .env.example .env        # then fill in your own values (see "Configuration")
dotnet build
dotnet run --no-build -- --report-trx                          # everything
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"   # by tag
```

Everything a run produces lands in `TestResults/`: the TUnit report and TRX, the Reqnroll report in `reqnroll/`, and Playwright traces in `traces/` (open one with `npx playwright show-trace <file>.zip`).

Some scenarios are expected to fail (`@known-defect`, bugs in the application) or are skipped (`@wip`, no admin account configured). That is intentional.

## Quality gates

| Where | What runs |
|---|---|
| Git hooks (local) | `pre-commit`: `dotnet format` check and Gherkin lint on staged files · `commit-msg`: Conventional Commits · `pre-push`: build with analyzers |
| `aqa-quality.yml` | on every push and pull request: no generated files tracked, format, build (warnings are errors), Gherkin lint, test discovery |
| `aqa-tests.yml` | the scenarios themselves, on demand (Actions → AQA Tests → Run workflow) |

Enable the hooks once per clone, from the repository root:

```bash
dotnet tool restore --tool-manifest aqa/restaurant-aqa-framework/.config/dotnet-tools.json
git config core.hooksPath aqa/restaurant-aqa-framework/.husky
```

The hooks only act on commits and pushes that touch `aqa/restaurant-aqa-framework`. Gherkin lint needs Node.js.

## Configuration

There is no shared environment and no committed `.env`: copy `.env.example` to `.env` and fill it in with the data of **your own** deployment (see "Testing your own deployment"). `.env` is git-ignored; never commit it.

Settings come from `.env` or real environment variables (variables win):

| Setting | Meaning |
|---|---|
| `UI_URL` | where the frontend is served |
| `API_URL` | base URL of the backend API, including the stage (for example `…/develop`) |
| `WAITER_USERNAME`, `WAITER_PASSWORD` | a waiter account that already exists in the environment under test |
| `TESTTABLE_ID`, `TESTTABLE_WAITER_*` | a table and the waiter assigned to it (only for scenarios that need them) |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | an admin account; without it the admin scenarios are skipped |

The full list, with defaults, is in `.env.example`.

## Testing your own deployment

### If you deployed the backend / frontend / DevOps homework

1. `cp .env.example .env` and set `UI_URL` and `API_URL` to your deployment (API URL includes the stage, for example `…/develop`).
2. Waiters cannot be created by signing up, so create a waiter (and, for the admin scenarios, an admin) in *your* environment and put its credentials in `WAITER_*` / `ADMIN_*`. Optionally add a table and its assigned waiter (`TESTTABLE_*`).
3. Run `dotnet build` and a small slice first (`--treenode-filter "/*/*/*/*[Category=smoke]"`).

### If you have not deployed your own homework

If you deployed the original application, test that: set `UI_URL` / `API_URL` as above. Otherwise point the framework at a comparable public site (Booking, Airbnb, OpenTable, …) and write all the layers for it yourself: Gherkin features, step definitions, Screenplay tasks and questions, page objects with locators, and API clients where the site has a usable API. Use only flows you are allowed to automate (no purchases, no real bookings, respect the site's terms), and set `UI_URL` to that site.

1. **Generate a skeleton with Playwright codegen** (see "Presetup instructions"): record one flow, for example search → open a result → check details. Save the generated code to a scratch file, it is only raw material.
2. **Split it across the layers**: locators and small actions go to `Ui/` page objects, the flow becomes a task in `Screenplay/Tasks/`, the read-backs become questions in `Screenplay/Questions/`, the business wording goes to `Features/` and thin glue to `StepDefinitions/`.
3. **Replace weak locators.** Codegen often emits positional or text-fragile selectors. Prefer role + accessible name, label, test id, then CSS (see [CLAUDE.md](CLAUDE.md)); check them in the Playwright inspector.
4. Tag scenarios `@ui` / `@api`, keep them independent, and run `npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"`.

### CI on your own public repository

Publish your work in your own public Git repository (the link is the deliverable). Workflows are disabled in a fresh fork until you enable them. Add the same values under Settings → Secrets and variables → Actions:

- **Variables:** `AQA_UI_URL`, `AQA_API_URL`, `AQA_TEST_LOCATION_ID`, `AQA_WAITER_LOCATION_ID`, `AQA_TESTTABLE_ID`
- **Secrets:** `AQA_WAITER_USERNAME`, `AQA_WAITER_PASSWORD`, `AQA_ADMIN_USERNAME`, `AQA_ADMIN_PASSWORD`, `AQA_TESTTABLE_WAITER_USERNAME`, `AQA_TESTTABLE_WAITER_PASSWORD`

`aqa-quality.yml` runs on every push and pull request; `aqa-tests.yml` (on demand) tests whatever those variables point at. Never put credentials in the repository, in `.env.example` or in test code.

## Presetup instructions

This framework contains PlaywriteInstaller script so you don't need to setup it manually.
For full BDD highlighting in your IDE you may need to install Reqnroll extension https://docs.reqnroll.net/latest/ide-integrations/index.html
To start the Playwright code generator open Powershell in the framework directory and enter the line:
```bash
bin/Debug/net10.0/playwright.ps1 codegen "<your UI_URL or the public site you test>"
```
Build first so `playwright.ps1` exists. The URL is optional.
