using NUnit.Framework;
using WarpBusiness.Tests.E2E.Pages;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class EmployeesTests : PageTestBase
{
    [SetUp]
    public async Task AuthenticateAsync()
    {
        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
    }

    [Test]
    public async Task EmployeesList_WhenAuthenticated_ShowsCorrectHeading()
    {
        await RequireAppAsync();

        var employees = new EmployeesPage(Page, BaseUrl);
        await employees.GoToAsync();
        await employees.WaitForPageReadyAsync();

        var heading = await employees.GetHeadingTextAsync();
        Assert.That(heading, Is.EqualTo("Employees"),
            "Page heading should be 'Employees'.");
    }

    [Test]
    public async Task EmployeesList_WhenAuthenticated_UrlIsCorrect()
    {
        await RequireAppAsync();

        var employees = new EmployeesPage(Page, BaseUrl);
        await employees.GoToAsync();
        await employees.WaitForPageReadyAsync();

        Assert.That(employees.CurrentUrl, Does.Contain("/employees"),
            "URL should contain /employees after navigation.");
    }

    [Test]
    public async Task EmployeesList_WhenUnauthenticated_RedirectsToLogin()
    {
        // Use a fresh page with no auth
        await Page.GotoAsync($"{BaseUrl}/employees");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2_000);

        Assert.That(Page.Url, Does.Contain("/login"),
            "Unauthenticated access to /employees should redirect to /login.");
    }
}
