using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

/// <summary>
/// E2E tests for the employee-user account linking feature.
/// These test UI elements being built by Geordi in parallel.
/// They will pass once the frontend components are in place.
/// </summary>
[TestFixture]
public class EmployeeUserLinkingE2ETests : PlaywrightTestBase
{
    [Test]
    public async Task EmployeeForm_ShowsUserAccountSection()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Click Add Employee to open the form
        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Employee" });
        await addButton.ClickAsync();

        // The form should contain a "User Account" section or heading
        var userAccountSection = Page.Locator("text=User Account").First;
        await Expect(userAccountSection).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task EmployeeForm_ChevronExpandsCreateUserSection()
    {
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        var addButton = Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add Employee" });
        await addButton.ClickAsync();

        // Look for the chevron/expand button in the User Account section
        var chevron = Page.Locator("[data-testid='expand-create-user'], .user-account-expand, button:has(svg.chevron), .bi-chevron-down").First;
        await Expect(chevron).ToBeVisibleAsync(new() { Timeout = 10000 });

        await chevron.ClickAsync();

        // After expanding, "Create User Account" fields should appear
        // (e.g., Role dropdown, or at minimum the section expands)
        var createUserFields = Page.Locator("text=Create User Account, text=Role, select[name='Role']").First;
        await Expect(createUserFields).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task EmployeeTable_ShowsLinkIndicator()
    {
        // If any employee is linked to a user account, the table should
        // show a visual indicator (icon, badge, or column value).
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Look for a column header or indicator related to user linking
        var linkIndicator = Page.Locator(
            "th:has-text('User'), " +
            "th:has-text('Linked'), " +
            "[data-testid='user-link-indicator'], " +
            ".bi-person-check, " +
            ".user-linked-icon"
        ).First;

        await Expect(linkIndicator).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task UserTable_EditLinkedUser_RedirectsToEmployees()
    {
        // When editing a user that is linked to an employee, the UI should
        // redirect to the employee edit page instead of the user edit page.
        await LoginAndSelectTenantAsync();
        await Page.GotoAsync($"{BaseUrl}/users");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Find a linked user's edit button (look for the link indicator in the row)
        var linkedUserRow = Page.Locator(
            "tr:has(.bi-person-check), " +
            "tr:has([data-testid='user-link-indicator']), " +
            "tr:has(.user-linked-icon)"
        ).First;

        // If no linked users exist in the test environment, skip gracefully
        if (!await linkedUserRow.IsVisibleAsync())
        {
            Assert.Ignore("No linked users found in test environment — " +
                "test requires seeded linked user data.");
            return;
        }

        var editButton = linkedUserRow.GetByRole(Microsoft.Playwright.AriaRole.Button,
            new() { NameRegex = new System.Text.RegularExpressions.Regex("Edit|Manage") });
        await editButton.ClickAsync();

        // Should redirect to employee edit, not user edit
        await Page.WaitForURLAsync(url => url.Contains("/employees/"),
            new() { Timeout = 10000 });
    }
}
