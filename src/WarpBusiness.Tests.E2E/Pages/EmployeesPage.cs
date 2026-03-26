using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E.Pages;

public class EmployeesPage(IPage page, string baseUrl)
{
    private readonly IPage _page = page;
    private readonly string _baseUrl = baseUrl;

    public async Task GoToAsync() =>
        await _page.GotoAsync($"{_baseUrl}/employees");

    public async Task WaitForPageReadyAsync() =>
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    public async Task<string> GetHeadingTextAsync() =>
        await _page.GetByRole(AriaRole.Heading, new() { Name = "Employees" }).InnerTextAsync();

    public async Task<bool> IsRedirectedToLoginAsync() =>
        _page.Url.Contains("/login");

    public string CurrentUrl => _page.Url;
}
