# WarpBusiness E2E Tests (Playwright)

End-to-end tests for the Warp Business Blazor frontend using [Microsoft.Playwright.NUnit](https://playwright.dev/dotnet/).

## Prerequisites

- .NET 10 SDK
- Playwright browsers installed (see below)
- Warp Business running locally (Aspire AppHost or standalone)

## Install Playwright browsers

```bash
dotnet build src/WarpBusiness.Tests.E2E/WarpBusiness.Tests.E2E.csproj
pwsh src/WarpBusiness.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install
```

The `PlaywrightSetup` fixture also runs `playwright install` automatically before the test suite begins.

## Environment variables

| Variable           | Default                  | Description                                   |
|--------------------|--------------------------|-----------------------------------------------|
| `E2E_BASE_URL`     | `http://localhost:5002`  | Base URL of the running Blazor app            |
| `E2E_TEST_EMAIL`   | `test@warp.local`        | Email for the test user (auto-created if new) |
| `E2E_TEST_PASSWORD`| `Test@123!`              | Password for the test user                    |

## Run against local Aspire instance

1. Start the AppHost:
   ```bash
   dotnet run --project src/WarpBusiness.AppHost
   ```

2. Find the Blazor web URL from Aspire dashboard (default: `http://localhost:5002`).

3. Run only E2E tests:
   ```bash
   dotnet test src/WarpBusiness.Tests.E2E/ --filter "Category=E2E"
   ```

4. Or run with a custom base URL:
   ```bash
   E2E_BASE_URL=http://localhost:5002 dotnet test src/WarpBusiness.Tests.E2E/ --filter "Category=E2E"
   ```
   On Windows (PowerShell):
   ```powershell
   $env:E2E_BASE_URL = "http://localhost:5002"
   dotnet test src/WarpBusiness.Tests.E2E/ --filter "Category=E2E"
   ```

## Exclude E2E from normal test runs

All test classes carry `[Category("E2E")]`. To exclude them from your normal CI `dotnet test`:

```bash
dotnet test --filter "Category!=E2E"
```

## Structure

```
WarpBusiness.Tests.E2E/
├── PlaywrightSetup.cs       # [SetUpFixture] — installs Playwright once per run
├── PageTestBase.cs          # Browser/context lifecycle base class
├── AuthHelper.cs            # Login / register helper (auto-registers on first run)
├── Pages/
│   ├── LoginPage.cs
│   ├── ContactsPage.cs
│   ├── CompaniesPage.cs
│   ├── DealsPage.cs
│   └── EmployeesPage.cs
└── Tests/
    ├── AuthTests.cs          # Login, invalid creds, register, unauthenticated redirect
    ├── ContactsTests.cs      # List view, columns, detail navigation
    ├── CompaniesTests.cs     # List view, columns, new button
    ├── DealsTests.cs         # List view, heading, new deal link
    ├── EmployeesTests.cs     # List view, unauthenticated redirect
    └── NavigationTests.cs    # Home page, nav menu links, sign-out
```

## Test user

The `AuthHelper` auto-creates the test user on first run via the `/register` page.
The default test credentials are `test@warp.local` / `Test@123!`. Override with env vars.

## Notes

- Tests skip with `Assert.Ignore` (not fail) if the app is unreachable — start the Aspire host first.
- Each test class gets a fresh browser context (cookies/storage cleared between tests).
- Selectors target semantic roles and label text rather than CSS classes to avoid brittleness.
