using NUnit.Framework;
using WarpBusiness.Tests.E2E.Pages;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class AuthTests : PageTestBase
{
    [Test]
    public async Task Login_WithValidCredentials_RedirectsToContacts()
    {
        await RequireAppAsync();

        var loginPage = new LoginPage(Page, BaseUrl);
        await loginPage.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        await loginPage.FillEmailAsync(TestEmail);
        await loginPage.FillPasswordAsync(TestPassword);
        await loginPage.ClickSignInAsync();

        // May need to register first; AuthHelper handles the fallback
        await Page.WaitForTimeoutAsync(2_000);

        if (Page.Url.Contains("/login"))
        {
            // Auto-register then retry login
            await AuthHelper.RegisterAsync(Page, BaseUrl, TestEmail, TestPassword);
            await loginPage.GoToAsync();
            await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
            await loginPage.FillEmailAsync(TestEmail);
            await loginPage.FillPasswordAsync(TestPassword);
            await loginPage.ClickSignInAsync();
            await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        }

        Assert.That(Page.Url, Does.Not.Contain("/login"),
            "Should have been redirected away from login after successful auth.");
    }

    [Test]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await RequireAppAsync();

        var loginPage = new LoginPage(Page, BaseUrl);
        await loginPage.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        await loginPage.FillEmailAsync("nobody@warp.local");
        await loginPage.FillPasswordAsync("WrongPassword99!");
        await loginPage.ClickSignInAsync();
        await Page.WaitForTimeoutAsync(2_000);

        var error = await loginPage.GetErrorMessageAsync();
        Assert.That(error, Is.Not.Null.And.Not.Empty,
            "Should display an error message for invalid credentials.");
    }

    [Test]
    public async Task Register_NewUser_RedirectsToContacts()
    {
        await RequireAppAsync();

        var uniqueEmail = $"e2e-{Guid.NewGuid():N}@warp.local";

        await AuthHelper.RegisterAsync(Page, BaseUrl, uniqueEmail, TestPassword);

        Assert.That(Page.Url, Does.Not.Contain("/register"),
            "Should have been redirected away from register page after successful registration.");
    }

    [Test]
    public async Task UnauthenticatedAccess_ToProtectedPage_RedirectsToLogin()
    {
        await RequireAppAsync();

        // Go directly to a protected page without logging in
        await Page.GotoAsync($"{BaseUrl}/contacts");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        // Blazor may redirect to login or show a challenge — URL should contain /login
        // Allow a moment for client-side redirect
        await Page.WaitForTimeoutAsync(2_000);

        Assert.That(Page.Url, Does.Contain("/login"),
            "Unauthenticated users should be redirected to /login.");
    }
}
