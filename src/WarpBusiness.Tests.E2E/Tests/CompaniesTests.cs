using NUnit.Framework;
using WarpBusiness.Tests.E2E.Pages;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class CompaniesTests : PageTestBase
{
    [SetUp]
    public async Task AuthenticateAsync()
    {
        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
    }

    [Test]
    public async Task CompaniesList_WhenAuthenticated_ShowsTable()
    {
        await RequireAppAsync();

        var companies = new CompaniesPage(Page, BaseUrl);
        await companies.GoToAsync();
        await companies.WaitForTableAsync();

        Assert.That(await companies.IsTableVisibleAsync(), Is.True,
            "Companies table should be visible when authenticated.");
    }

    [Test]
    public async Task CompaniesList_WhenAuthenticated_ShowsCorrectHeading()
    {
        await RequireAppAsync();

        var companies = new CompaniesPage(Page, BaseUrl);
        await companies.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        var heading = await companies.GetHeadingTextAsync();
        Assert.That(heading, Is.EqualTo("Companies"),
            "Page heading should be 'Companies'.");
    }

    [Test]
    public async Task CompaniesList_WhenAuthenticated_ShowsExpectedColumns()
    {
        await RequireAppAsync();

        var companies = new CompaniesPage(Page, BaseUrl);
        await companies.GoToAsync();
        await companies.WaitForTableAsync();

        var headers = await companies.GetColumnHeadersAsync();
        Assert.That(headers, Does.Contain("Name"), "Table should have a Name column.");
        Assert.That(headers, Does.Contain("Industry"), "Table should have an Industry column.");
        Assert.That(headers, Does.Contain("Contacts"), "Table should have a Contacts column.");
    }

    [Test]
    public async Task CompaniesList_WhenAuthenticated_HasNewCompanyButton()
    {
        await RequireAppAsync();

        var companies = new CompaniesPage(Page, BaseUrl);
        await companies.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        Assert.That(await companies.HasNewCompanyButtonAsync(), Is.True,
            "New Company button should be visible.");
    }
}
