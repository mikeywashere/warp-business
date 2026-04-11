using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class TenantSelectorTests : PlaywrightTestBase
{
    [Test]
    public async Task AfterLogin_TenantSelectorLoadsWithAvailableTenants()
    {
        await LoginAsync();

        // Wait for Blazor InteractiveServer to render tenant cards
        await Page.WaitForSelectorAsync(".tenant-card", new() { Timeout = 10000 });

        // At least one tenant card should be visible
        var tenantCards = Page.Locator(".tenant-card");
        var count = await tenantCards.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one tenant card on the selector page");
    }

    [Test]
    public async Task ClickingTenantCard_SelectsAndRedirectsToHome()
    {
        await LoginAsync();

        await Page.WaitForSelectorAsync(".tenant-card", new() { Timeout = 10000 });

        // Click the Warp Industries tenant card
        var tenantCard = Page.Locator(".tenant-card", new() { HasText = DefaultTenantName }).First;
        await Expect(tenantCard).ToBeVisibleAsync();
        await tenantCard.ClickAsync();

        // Should redirect to home page
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return uri.AbsolutePath == "/" || uri.AbsolutePath == "";
        }, new() { Timeout = 15000 });
    }

    [Test]
    public async Task SelectedTenant_AppearsInLayoutHeader()
    {
        await LoginAndSelectTenantAsync();

        // MainLayout shows "Working in: <TenantName> [Switch]" in top-row
        var tenantIndicator = Page.Locator(".top-row").GetByText("Working in:");
        await Expect(tenantIndicator).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task SwitchTenantLink_NavigatesBackToSelector()
    {
        await LoginAndSelectTenantAsync();

        // Click [Switch] link in the top bar
        var switchLink = Page.Locator(".top-row a[href='/select-tenant']");
        await Expect(switchLink).ToBeVisibleAsync();
        await switchLink.ClickAsync();

        // Should be back on tenant selector
        await Page.WaitForURLAsync(url => url.Contains("/select-tenant"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveTitleAsync("Select Organization");
    }

    [Test]
    public async Task TenantCard_DisplaysTenantNameAndSlug()
    {
        await LoginAsync();

        await Page.WaitForSelectorAsync(".tenant-card", new() { Timeout = 10000 });

        // Each tenant card should have a title (h5) and slug (small.text-muted)
        var firstCard = Page.Locator(".tenant-card").First;
        var cardTitle = firstCard.Locator(".card-title");
        var cardSlug = firstCard.Locator("small.text-muted");

        await Expect(cardTitle).ToBeVisibleAsync();
        await Expect(cardSlug).ToBeVisibleAsync();

        // Title should not be empty
        var titleText = await cardTitle.TextContentAsync();
        Assert.That(titleText, Is.Not.Null.And.Not.Empty);
    }
}
