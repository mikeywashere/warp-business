using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class EmployeeManagementTests : PlaywrightTestBase
{
    [Test]
    public async Task AuthenticatedUser_SeesEmployeesInModulesDropdown()
    {
        await LoginAndSelectTenantAsync();

        // The "Modules" dropdown should contain an "Employees" link
        var modulesDropdown = Page.Locator("nav").GetByText("Modules");
        await Expect(modulesDropdown).ToBeVisibleAsync();

        await modulesDropdown.ClickAsync();

        var employeesLink = Page.Locator("a[href='employees']");
        await Expect(employeesLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task EmployeesNavLink_RoutesToCorrectPage()
    {
        await LoginAndSelectTenantAsync();

        // Open Modules dropdown and click Employees
        var modulesDropdown = Page.Locator("nav").GetByText("Modules");
        await modulesDropdown.ClickAsync();

        var employeesLink = Page.Locator("a[href='employees']");
        await employeesLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/employees"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveTitleAsync("Employees");
    }

    [Test]
    public async Task EmployeeManagement_ShowsEmptyStateInitially()
    {
        await LoginAndSelectTenantAsync();

        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Page should have a table or empty state and an "Add Employee" button
        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Employee" });
        await Expect(addButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddEmployee_CreatesNewEmployee()
    {
        await LoginAndSelectTenantAsync();

        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Click Add Employee
        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Employee" });
        await addButton.ClickAsync();

        // Fill out form fields
        await Page.Locator("input[name='FirstName'], #FirstName, input[placeholder*='First']").First.FillAsync("Geordi");
        await Page.Locator("input[name='LastName'], #LastName, input[placeholder*='Last']").First.FillAsync("La Forge");
        await Page.Locator("input[name='Email'], #Email, input[placeholder*='Email']").First.FillAsync($"laforge-{Guid.NewGuid():N}@enterprise.com");

        // Submit the form
        var submitButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Save|Create|Submit|Add") });
        await submitButton.ClickAsync();

        // Verify the employee appears in the table
        var tableRow = Page.Locator("table").GetByText("La Forge");
        await Expect(tableRow).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task UnauthenticatedUser_DoesNotSeeModulesDropdown()
    {
        await Page.GotoAsync(BaseUrl);

        // Modules dropdown should not be visible for unauthenticated users
        var modulesDropdown = Page.Locator("nav").GetByText("Modules");
        await Expect(modulesDropdown).ToHaveCountAsync(0);
    }
}
