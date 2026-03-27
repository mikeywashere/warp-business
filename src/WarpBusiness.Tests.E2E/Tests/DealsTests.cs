using NUnit.Framework;
using WarpBusiness.Tests.E2E.Pages;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class DealsTests : PageTestBase
{
    [SetUp]
    public async Task AuthenticateAsync()
    {
        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
    }

    [Test]
    public async Task DealsList_WhenAuthenticated_ShowsCorrectHeading()
    {
        await RequireAppAsync();

        var deals = new DealsPage(Page, BaseUrl);
        await deals.GoToAsync();
        await deals.WaitForPageReadyAsync();

        var heading = await deals.GetHeadingTextAsync();
        Assert.That(heading, Is.EqualTo("Deals"),
            "Page heading should be 'Deals'.");
    }

    [Test]
    public async Task DealsList_WhenAuthenticated_HasNewDealLink()
    {
        await RequireAppAsync();

        var deals = new DealsPage(Page, BaseUrl);
        await deals.GoToAsync();
        await deals.WaitForPageReadyAsync();

        Assert.That(await deals.HasNewDealLinkAsync(), Is.True,
            "New Deal link should be visible.");
    }

    [Test]
    public async Task DealsList_WhenAuthenticated_UrlIsCorrect()
    {
        await RequireAppAsync();

        var deals = new DealsPage(Page, BaseUrl);
        await deals.GoToAsync();
        await deals.WaitForPageReadyAsync();

        Assert.That(deals.CurrentUrl, Does.Contain("/deals"),
            "URL should contain /deals after navigation.");
    }
}
