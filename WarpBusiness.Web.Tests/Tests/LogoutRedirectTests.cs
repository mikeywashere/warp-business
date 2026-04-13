using NUnit.Framework;
using WarpBusiness.Web.Tests.Infrastructure;

namespace WarpBusiness.Web.Tests.Tests;

[TestFixture]
public class LogoutRedirectTests : PlaywrightTestBase
{
    [Test]
    public async Task SetTenant_WritesLastTenantSlugCookie()
    {
        // Arrange: Login to get authenticated
        await LoginAsync();

        // Act: Navigate to set-tenant endpoint with specific tenant
        var testTenantId = Guid.NewGuid();
        var testTenantName = "Test Organization";
        await Page.GotoAsync($"{BaseUrl}/set-tenant?tenantId={testTenantId}&tenantName={testTenantName}&returnUrl=/");

        // Wait for redirect to complete
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return uri.AbsolutePath == "/" || uri.AbsolutePath == "";
        }, new() { Timeout = 5000 });

        // Assert: Verify X-Last-Tenant-Slug cookie was set
        var cookies = await Page.Context.CookiesAsync();
        var lastTenantCookie = cookies.FirstOrDefault(c => c.Name == "X-Last-Tenant-Slug");

        Assert.That(lastTenantCookie, Is.Not.Null, "X-Last-Tenant-Slug cookie should be set");
        Assert.That(lastTenantCookie!.Value, Is.EqualTo(testTenantName), "Cookie should contain the tenant name");
        Assert.That(lastTenantCookie.HttpOnly, Is.True, "Cookie should be HttpOnly");
        Assert.That(lastTenantCookie.SameSite, Is.EqualTo(Microsoft.Playwright.SameSiteAttribute.Strict), "Cookie should be SameSite=Strict");
        Assert.That(lastTenantCookie.Path, Is.EqualTo("/"), "Cookie path should be /");

        // Verify long expiration (365 days, allow 1 hour tolerance)
        var expectedExpiry = DateTimeOffset.UtcNow.AddDays(365);
        var actualExpiry = DateTimeOffset.FromUnixTimeSeconds((long)lastTenantCookie.Expires);
        var expiryDifference = Math.Abs((actualExpiry - expectedExpiry).TotalHours);
        Assert.That(expiryDifference, Is.LessThan(1), "Cookie should expire in approximately 365 days");
    }

    [Test]
    public async Task Logout_DeletesIdTokenCacheButKeepsLastTenantSlug()
    {
        // Arrange: Login and select tenant (this will set both cookies)
        await LoginAndSelectTenantAsync();

        // Verify setup: Both cookies should exist after login
        var cookiesBeforeLogout = await Page.Context.CookiesAsync();
        var lastTenantBeforeLogout = cookiesBeforeLogout.FirstOrDefault(c => c.Name == "X-Last-Tenant-Slug");
        Assert.That(lastTenantBeforeLogout, Is.Not.Null, "X-Last-Tenant-Slug should exist before logout");

        // Note: X-IdToken-Cache is set during OIDC flow and is HttpOnly, so we can't directly verify it
        // But we can verify the behavior after logout

        // Act: Perform logout
        await Page.GotoAsync($"{BaseUrl}/logout");

        // Wait for logout to complete (redirects to home)
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return !uri.AbsolutePath.Contains("/logout");
        }, new() { Timeout = 15000 });

        // Assert: Verify X-Last-Tenant-Slug persists after logout
        var cookiesAfterLogout = await Page.Context.CookiesAsync();
        var lastTenantAfterLogout = cookiesAfterLogout.FirstOrDefault(c => c.Name == "X-Last-Tenant-Slug");

        Assert.That(lastTenantAfterLogout, Is.Not.Null, "X-Last-Tenant-Slug should persist after logout");
        Assert.That(lastTenantAfterLogout!.Value, Is.EqualTo(lastTenantBeforeLogout!.Value), 
            "X-Last-Tenant-Slug value should remain unchanged");

        // Verify X-IdToken-Cache was deleted (should not be present)
        var idTokenCache = cookiesAfterLogout.FirstOrDefault(c => c.Name == "X-IdToken-Cache");
        Assert.That(idTokenCache, Is.Null, "X-IdToken-Cache should be deleted after logout");
    }

    [Test]
    public async Task Logout_DeletesSelectedTenantCookies()
    {
        // Arrange: Login and select tenant
        await LoginAndSelectTenantAsync();

        // Verify setup: Selected tenant cookies should exist
        var cookiesBeforeLogout = await Page.Context.CookiesAsync();
        var selectedTenantBefore = cookiesBeforeLogout.FirstOrDefault(c => c.Name == "X-Selected-Tenant");
        var selectedTenantNameBefore = cookiesBeforeLogout.FirstOrDefault(c => c.Name == "X-Selected-Tenant-Name");

        Assert.That(selectedTenantBefore, Is.Not.Null, "X-Selected-Tenant should exist before logout");
        Assert.That(selectedTenantNameBefore, Is.Not.Null, "X-Selected-Tenant-Name should exist before logout");

        // Act: Perform logout
        await Page.GotoAsync($"{BaseUrl}/logout");

        // Wait for logout to complete
        await Page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return !uri.AbsolutePath.Contains("/logout");
        }, new() { Timeout = 15000 });

        // Assert: Verify selected tenant cookies were deleted
        var cookiesAfterLogout = await Page.Context.CookiesAsync();
        var selectedTenantAfter = cookiesAfterLogout.FirstOrDefault(c => c.Name == "X-Selected-Tenant");
        var selectedTenantNameAfter = cookiesAfterLogout.FirstOrDefault(c => c.Name == "X-Selected-Tenant-Name");

        Assert.That(selectedTenantAfter, Is.Null, "X-Selected-Tenant should be deleted after logout");
        Assert.That(selectedTenantNameAfter, Is.Null, "X-Selected-Tenant-Name should be deleted after logout");
    }
}
