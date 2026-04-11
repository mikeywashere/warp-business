using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class NavigationTests : PlaywrightTestBase
{
    [Test]
    public async Task AuthenticatedUser_SeesUserManagementInNav()
    {
        await LoginAndSelectTenantAsync();

        // NavMenu shows "User Management" for authenticated users
        var navLink = Page.Locator("nav a[href='users']");
        await Expect(navLink).ToBeVisibleAsync();
        await Expect(navLink).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("User Management"));
    }

    [Test]
    public async Task AuthenticatedAdmin_SeesTenantManagementInNav()
    {
        await LoginAndSelectTenantAsync();

        // NavMenu shows "Tenant Management" for SystemAdministrator role
        var navLink = Page.Locator("nav a[href='tenants']");
        await Expect(navLink).ToBeVisibleAsync();
        await Expect(navLink).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Tenant Management"));
    }

    [Test]
    public async Task UserManagementNavLink_RoutesToCorrectPage()
    {
        await LoginAndSelectTenantAsync();

        // Click User Management nav link
        var navLink = Page.Locator("nav a[href='users']");
        await navLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/users"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveTitleAsync("User Management");
    }

    [Test]
    public async Task TenantManagementNavLink_RoutesToCorrectPage()
    {
        await LoginAndSelectTenantAsync();

        // Click Tenant Management nav link
        var navLink = Page.Locator("nav a[href='tenants']");
        await navLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/tenants"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveTitleAsync("Tenant Management");
    }

    [Test]
    public async Task HomeNavLink_RoutesToHomePage()
    {
        await LoginAndSelectTenantAsync();

        // Navigate away first
        await Page.GotoAsync($"{BaseUrl}/users");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });

        // Click Home nav link
        var navLink = Page.Locator("nav a[href='']").First;
        await navLink.ClickAsync();

        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return uri.AbsolutePath == "/" || uri.AbsolutePath == "";
        }, new() { Timeout = 10000 });

        await Expect(Page).ToHaveTitleAsync("Home");
    }

    [Test]
    public async Task UnauthenticatedUser_DoesNotSeeUserManagementInNav()
    {
        await Page.GotoAsync(BaseUrl);

        // Nav should not show "User Management" for unauthenticated users
        var navLink = Page.Locator("nav a[href='users']");
        await Expect(navLink).ToHaveCountAsync(0);
    }

    [Test]
    public async Task UnauthenticatedUser_DoesNotSeeTenantManagementInNav()
    {
        await Page.GotoAsync(BaseUrl);

        // Nav should not show "Tenant Management" for unauthenticated users
        var navLink = Page.Locator("nav a[href='tenants']");
        await Expect(navLink).ToHaveCountAsync(0);
    }
}
