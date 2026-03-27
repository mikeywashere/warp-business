using Microsoft.Playwright;
using NUnit.Framework;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class NavigationTests : PageTestBase
{
    [Test]
    public async Task HomePage_WhenUnauthenticated_LoadsSuccessfully()
    {
        await RequireAppAsync();

        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Warp Business"),
            "Home page title should contain 'Warp Business'.");
    }

    [Test]
    public async Task HomePage_WhenUnauthenticated_ShowsSignInButton()
    {
        await RequireAppAsync();

        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var signIn = Page.GetByRole(AriaRole.Link, new() { Name = "Sign In" });
        Assert.That(await signIn.IsVisibleAsync(), Is.True,
            "Sign In link should be visible on home page when not authenticated.");
    }

    [Test]
    public async Task NavMenu_WhenAuthenticated_ShowsSignOutButton()
    {
        await RequireAppAsync();

        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var signOut = Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" });
        Assert.That(await signOut.IsVisibleAsync(), Is.True,
            "Sign out button should appear in the nav when authenticated.");
    }

    [Test]
    public async Task NavMenu_Brand_IsVisible()
    {
        await RequireAppAsync();

        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var brand = Page.GetByText("Warp Business").First;
        Assert.That(await brand.IsVisibleAsync(), Is.True,
            "'Warp Business' brand text should appear in the nav menu.");
    }

    [Test]
    public async Task NavMenu_WhenAuthenticated_HomeLink_IsPresent()
    {
        await RequireAppAsync();

        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var homeLink = Page.GetByRole(AriaRole.Link, new() { Name = "Home" });
        Assert.That(await homeLink.IsVisibleAsync(), Is.True,
            "Home link should be visible in the nav menu.");
    }

    [Test]
    public async Task NavMenu_WhenAuthenticated_SignOut_NavigatesToLogin()
    {
        await RequireAppAsync();

        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/login"),
            "Clicking Sign out should navigate to the login page.");
    }

    [Test]
    public async Task HomePage_WhenAuthenticated_ShowsQuickLinks()
    {
        await RequireAppAsync();

        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(
            await Page.GetByRole(AriaRole.Link, new() { Name = "Contacts" }).IsVisibleAsync(),
            Is.True, "Contacts quick-link should appear on home page when authenticated.");

        Assert.That(
            await Page.GetByRole(AriaRole.Link, new() { Name = "Companies" }).IsVisibleAsync(),
            Is.True, "Companies quick-link should appear on home page when authenticated.");

        Assert.That(
            await Page.GetByRole(AriaRole.Link, new() { Name = "Deals" }).IsVisibleAsync(),
            Is.True, "Deals quick-link should appear on home page when authenticated.");
    }
}
