using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class HomePageTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_LoadsWithoutErrors()
    {
        var response = await Page.GotoAsync(BaseUrl);

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Ok, Is.True);
    }

    [Test]
    public async Task HomePage_HasCorrectTitle()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page).ToHaveTitleAsync("Home");
    }

    [Test]
    public async Task HomePage_UnauthenticatedUser_SeesLoginButton()
    {
        await Page.GotoAsync(BaseUrl);

        // Home.razor shows "Login" link in <NotAuthorized> block
        var loginLink = Page.Locator("a[href='/login']");
        await Expect(loginLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task HomePage_UnauthenticatedUser_SeesNotLoggedInMessage()
    {
        await Page.GotoAsync(BaseUrl);

        var message = Page.GetByText("You are not logged in.");
        await Expect(message).ToBeVisibleAsync();
    }
}
