using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class LoginFlowTests : PlaywrightTestBase
{
    [Test]
    public async Task LoginButton_RedirectsToKeycloak()
    {
        await Page.GotoAsync(BaseUrl);

        // Click the Login link
        await Page.Locator("a[href='/login']").ClickAsync();

        // Should redirect to Keycloak login page
        await Page.WaitForURLAsync(url => url.Contains("realms/warpbusiness"), new() { Timeout = 15000 });

        // Keycloak form fields should be present
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
        await Expect(Page.Locator("#password")).ToBeVisibleAsync();
        await Expect(Page.Locator("#kc-login")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ValidCredentials_CompletesOidcFlow_RedirectsToTenantSelector()
    {
        await LoginAsync();

        // Verify we landed on tenant selector
        Assert.That(Page.Url, Does.Contain("/select-tenant"));
    }

    [Test]
    public async Task AfterLogin_UserSeesTenantSelector_NotHomePage()
    {
        await LoginAsync();

        // Page title should be "Select Organization" (from TenantSelector.razor)
        await Expect(Page).ToHaveTitleAsync("Select Organization");

        // The heading should be visible
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Select Organization" });
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task Logout_RedirectsToHomeAndClearsSession()
    {
        await LoginAndSelectTenantAsync();

        // Verify we're authenticated — should see logout link
        var logoutLink = Page.Locator("a[href='/logout']");
        await Expect(logoutLink).ToBeVisibleAsync();

        // Click logout
        await logoutLink.ClickAsync();

        // After logout, should be back at home page without auth state
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return !uri.AbsolutePath.Contains("/select-tenant") && !uri.AbsolutePath.Contains("/logout");
        }, new() { Timeout = 15000 });

        // Should see login button again (unauthenticated)
        await Page.GotoAsync(BaseUrl);
        var loginLink = Page.Locator("a[href='/login']");
        await Expect(loginLink).ToBeVisibleAsync();
    }
}
