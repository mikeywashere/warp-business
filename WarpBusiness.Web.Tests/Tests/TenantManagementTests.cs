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

    [Test]
    public async Task TenantTable_HasCurrencyColumnHeader()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // The table header row should include all 6 columns: Name, Slug, Currency, Status, Created, Actions
        var headers = Page.Locator("table.table thead th");
        var headerCount = await headers.CountAsync();
        Assert.That(headerCount, Is.EqualTo(6), "Expected 6 column headers (Name, Slug, Currency, Status, Created, Actions)");

        var currencyHeader = Page.Locator("table.table thead th", new() { HasText = "Currency" });
        await Expect(currencyHeader).ToBeVisibleAsync();
    }

    [Test]
    public async Task TenantTable_ShowsCurrencyDataForTenants()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // The third column (index 2) in each tenant row should contain currency data or the em-dash placeholder
        var firstRowCurrencyCell = Page.Locator("table.table tbody tr:first-child td:nth-child(3)");
        await Expect(firstRowCurrencyCell).ToBeVisibleAsync();

        var cellText = await firstRowCurrencyCell.TextContentAsync();
        Assert.That(cellText, Is.Not.Null.And.Not.Empty,
            "Currency cell should display a currency code or placeholder");
    }

    [Test]
    public async Task TenantTable_ColspanMatchesColumnCount()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // Expand the members panel for the first tenant to expose the colspan row
        var membersButton = Page.Locator("table.table tbody tr:first-child button", new() { HasText = "Members" });
        await Expect(membersButton).ToBeVisibleAsync();
        await membersButton.ClickAsync();

        // The members panel row uses a td[colspan] — verify it matches the 6-column header
        var colspanCell = Page.Locator("table.table > tbody > tr td[colspan]").First;
        await Expect(colspanCell).ToBeVisibleAsync();

        var colspanValue = await colspanCell.GetAttributeAsync("colspan");
        Assert.That(colspanValue, Is.EqualTo("6"),
            "Members panel colspan must match the 6-column table header (Name, Slug, Currency, Status, Created, Actions)");
    }

    [Test]
    public async Task TenantTable_ActionButtonsHaveConsistentSpacing()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/tenants");
        await Page.WaitForSelectorAsync("table.table", new() { Timeout = 10000 });

        // All three action buttons (Members, Edit, Delete) should have the me-1 spacing class
        var firstRowActions = Page.Locator("table.table tbody tr:first-child td:last-child");
        await Expect(firstRowActions).ToBeVisibleAsync();

        var membersBtn = firstRowActions.Locator("button", new() { HasText = "Members" });
        var editBtn = firstRowActions.Locator("button", new() { HasText = "Edit" });
        var deleteBtn = firstRowActions.Locator("button", new() { HasText = "Delete" });

        await Expect(membersBtn).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bme-1\b"));
        await Expect(editBtn).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bme-1\b"));
        await Expect(deleteBtn).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bme-1\b"));
    }
}
