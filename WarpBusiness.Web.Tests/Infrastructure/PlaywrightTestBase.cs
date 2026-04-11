using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace WarpBusiness.Web.Tests.Infrastructure;

/// <summary>
/// Base class for all Playwright E2E tests.
/// Provides login helpers and common configuration for the WarpBusiness app.
/// Tests run against a live Aspire environment (web + API + PostgreSQL + Keycloak).
/// </summary>
public class PlaywrightTestBase : PageTest
{
    protected string BaseUrl => Environment.GetEnvironmentVariable("APP_URL") ?? "https://localhost:5001";

    // Keycloak test credentials
    protected const string TestUsername = "michael.schmidt";
    protected const string TestPassword = "Warp2026!xK9";
    protected const string DefaultTenantName = "Warp Industries";

    /// <summary>
    /// Logs in via Keycloak OIDC flow and lands on the tenant selector page.
    /// </summary>
    protected async Task LoginAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Keycloak login form
        await Page.WaitForURLAsync(url => url.Contains("realms/warpbusiness"), new() { Timeout = 15000 });
        await Page.Locator("#username").FillAsync(TestUsername);
        await Page.Locator("#password").FillAsync(TestPassword);
        await Page.Locator("#kc-login").ClickAsync();

        // Should redirect to tenant selector after login
        await Page.WaitForURLAsync(url => url.Contains("/select-tenant"), new() { Timeout = 15000 });
    }

    /// <summary>
    /// Logs in and selects the default tenant ("Warp Industries"), ending on the home page.
    /// </summary>
    protected async Task LoginAndSelectTenantAsync()
    {
        await LoginAsync();

        // Wait for tenant cards to render (Blazor InteractiveServer)
        await Page.WaitForSelectorAsync(".tenant-card", new() { Timeout = 10000 });

        // Find the card containing "Warp Industries" and click it
        var tenantCard = Page.Locator(".tenant-card", new() { HasText = DefaultTenantName }).First;

        if (await tenantCard.IsVisibleAsync())
        {
            await tenantCard.ClickAsync();
        }

        // Should end up on home page after tenant selection (forceLoad: true causes full reload)
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return uri.AbsolutePath == "/" || uri.AbsolutePath == "";
        }, new() { Timeout = 15000 });
    }
}
