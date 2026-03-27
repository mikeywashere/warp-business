using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E.Pages;

public class CompaniesPage(IPage page, string baseUrl)
{
    private readonly IPage _page = page;
    private readonly string _baseUrl = baseUrl;

    public async Task GoToAsync() =>
        await _page.GotoAsync($"{_baseUrl}/companies");

    public async Task WaitForTableAsync() =>
        await _page.WaitForSelectorAsync("table.table", new() { Timeout = 10_000 });

    public async Task<bool> IsTableVisibleAsync() =>
        await _page.Locator("table.table").IsVisibleAsync();

    public async Task<string> GetHeadingTextAsync() =>
        await _page.GetByRole(AriaRole.Heading, new() { Name = "Companies" }).InnerTextAsync();

    public async Task<IReadOnlyList<string>> GetColumnHeadersAsync()
    {
        var headers = _page.Locator("table.table thead th");
        var count = await headers.CountAsync();
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var text = (await headers.Nth(i).InnerTextAsync()).Trim();
            if (!string.IsNullOrEmpty(text))
                result.Add(text);
        }
        return result;
    }

    public async Task<bool> HasNewCompanyButtonAsync() =>
        await _page.GetByRole(AriaRole.Button, new() { Name = "New Company" }).IsVisibleAsync();

    public async Task FillSearchAsync(string text) =>
        await _page.GetByPlaceholder("Search companies...").FillAsync(text);

    public string CurrentUrl => _page.Url;
}
