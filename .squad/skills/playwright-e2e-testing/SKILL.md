# Skill: Playwright E2E Testing for Blazor + Keycloak

## When to Use

When you need browser-based end-to-end tests for a Blazor Server app with OIDC authentication (Keycloak).

## Setup

1. Create NUnit test project (Playwright .NET requires NUnit):
   ```xml
   <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
   <PackageReference Include="Microsoft.Playwright" Version="1.*" />
   <PackageReference Include="Microsoft.Playwright.NUnit" Version="1.*" />
   ```

2. Create `.runsettings` for headless Chromium.

3. Base class extends `PageTest` (from `Microsoft.Playwright.NUnit`).

4. Install browsers after build:
   ```
   pwsh bin/Debug/net10.0/playwright.ps1 install
   ```

## Key Patterns

### Keycloak OIDC Login Helper
```csharp
await Page.GotoAsync($"{BaseUrl}/login");
await Page.WaitForURLAsync(url => url.Contains("realms/{realm}"));
await Page.Locator("#username").FillAsync(username);
await Page.Locator("#password").FillAsync(password);
await Page.Locator("#kc-login").ClickAsync();
await Page.WaitForURLAsync(url => url.Contains("/post-login-page"));
```

### Blazor InteractiveServer Content
Use `WaitForSelectorAsync` for dynamically rendered content:
```csharp
await Page.WaitForSelectorAsync(".my-component", new() { Timeout = 10000 });
```

### Locator Strategy (no data-testid)
- CSS classes: `Page.Locator(".tenant-card")`
- Text content: `Page.Locator(".card", new() { HasText = "My Text" })`
- Roles: `Page.GetByRole(AriaRole.Heading, new() { Name = "Title" })`
- Nav links: `Page.Locator("nav a[href='page-name']")`

## Gotchas

- Playwright for .NET uses NUnit, NOT xUnit
- All test files need `using NUnit.Framework;` for `[TestFixture]` and `[Test]` attributes
- `PageTest` provides `Page` and `Expect()` — don't create your own browser/page instances
- Blazor `forceLoad: true` navigation causes full page reload — use URL-based waits, not SPA route detection
