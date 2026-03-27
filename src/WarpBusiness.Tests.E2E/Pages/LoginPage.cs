using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E.Pages;

public class LoginPage(IPage page, string baseUrl)
{
    private readonly IPage _page = page;
    private readonly string _baseUrl = baseUrl;

    public async Task GoToAsync() =>
        await _page.GotoAsync($"{_baseUrl}/login");

    public async Task FillEmailAsync(string email) =>
        await _page.GetByLabel("Email").FillAsync(email);

    public async Task FillPasswordAsync(string password) =>
        await _page.GetByLabel("Password").FillAsync(password);

    public async Task ClickSignInAsync() =>
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

    public async Task<string?> GetErrorMessageAsync()
    {
        var alert = _page.Locator(".alert-danger");
        if (await alert.CountAsync() == 0) return null;
        return await alert.InnerTextAsync();
    }

    public async Task WaitForRedirectAwayAsync() =>
        await _page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });

    public string CurrentUrl => _page.Url;
}
