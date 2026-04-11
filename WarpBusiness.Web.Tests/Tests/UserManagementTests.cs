using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class UserManagementTests : PlaywrightTestBase
{
    [Test]
    public async Task AuthenticatedAdmin_CanNavigateToUsers()
    {
        await LoginAndSelectTenantAsync();

        await Page.GotoAsync($"{BaseUrl}/users");

        await Expect(Page).ToHaveTitleAsync("User Management");
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "User Management" });
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task UserList_LoadsAndShowsUsersInTable()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/users");

        // Wait for the table to render (Blazor InteractiveServer)
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Table should have at least one data row
        var rows = Page.Locator("table.table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one user in the table");
    }

    [Test]
    public async Task AddUserButton_ShowsForm()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/users");

        // Wait for loading to complete
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Click "Add User" button
        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add User" });
        await Expect(addButton).ToBeVisibleAsync();
        await addButton.ClickAsync();

        // The form card should appear with "Add User" header
        var formHeader = Page.Locator(".card-header").GetByText("Add User");
        await Expect(formHeader).ToBeVisibleAsync();

        // Form fields should be visible
        await Expect(Page.Locator("input").Nth(0)).ToBeVisibleAsync(); // First Name
        await Expect(Page.Locator("input[type='email']")).ToBeVisibleAsync(); // Email
    }

    [Test]
    public async Task UserTable_HasExpectedColumns()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/users");

        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Verify table headers: Name, Email, Role, Created, Actions
        var headers = Page.Locator("table.table thead th");
        var headerTexts = await headers.AllTextContentsAsync();

        Assert.That(headerTexts, Does.Contain("Name"));
        Assert.That(headerTexts, Does.Contain("Email"));
        Assert.That(headerTexts, Does.Contain("Role"));
    }

    [Test]
    public async Task UnauthenticatedUser_SeesAccessDeniedOnUsersPage()
    {
        await Page.GotoAsync($"{BaseUrl}/users");

        // Unauthenticated users get redirected to login or see access denied
        // The page uses AuthorizeView, so it may show the NotAuthorized content
        // or the app may redirect to login via the [Authorize] attribute
        var currentUrl = Page.Url;

        // Either redirected to Keycloak login or shows access denied
        var isRedirectedToLogin = currentUrl.Contains("realms/warpbusiness") || currentUrl.Contains("/login");
        var accessDenied = Page.GetByText("Access Denied");
        var showsAccessDenied = await accessDenied.IsVisibleAsync().ContinueWith(t => t.IsCompletedSuccessfully && t.Result);

        Assert.That(isRedirectedToLogin || showsAccessDenied, Is.True,
            "Expected either redirect to login or access denied message");
    }
}
