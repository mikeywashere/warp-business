using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class TenantManagementTests : PlaywrightTestBase
{
    [Test]
    public async Task AuthenticatedAdmin_CanNavigateToTenants()
    {
        await LoginAndSelectTenantAsync();

        await Page.GotoAsync($"{BaseUrl}/tenants");

        await Expect(Page).ToHaveTitleAsync("Tenant Management");
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Tenant Management" });
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task TenantList_LoadsAndShowsTenantsInTable()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");

        // Wait for the tenant table to render
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Table should have at least one tenant (Warp Industries)
        var rows = Page.Locator("table.table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one tenant in the table");
    }

    [Test]
    public async Task CreateTenantForm_CanBeOpened()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");

        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Click "Add Tenant" button
        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Tenant" });
        await Expect(addButton).ToBeVisibleAsync();
        await addButton.ClickAsync();

        // Form card should appear
        var formHeader = Page.Locator(".card-header").GetByText("Add Tenant");
        await Expect(formHeader).ToBeVisibleAsync();
    }

    [Test]
    public async Task TenantManagement_RequiresSystemAdministratorRole()
    {
        // Navigate to /tenants without logging in
        await Page.GotoAsync($"{BaseUrl}/tenants");

        // Should be redirected to Keycloak login (due to [Authorize(Roles = "SystemAdministrator")])
        var currentUrl = Page.Url;
        var isRedirectedToLogin = currentUrl.Contains("realms/warpbusiness") || currentUrl.Contains("/login");

        Assert.That(isRedirectedToLogin, Is.True,
            "Expected redirect to login for unauthenticated users accessing tenant management");
    }
}
