using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E.Pages;

public class ContactsPage(IPage page, string baseUrl)
{
    private readonly IPage _page = page;
    private readonly string _baseUrl = baseUrl;

    public async Task GoToAsync() =>
        await _page.GotoAsync($"{_baseUrl}/contacts");

    public async Task WaitForTableAsync() =>
        await _page.WaitForSelectorAsync("table.table", new() { Timeout = 10_000 });

    public async Task<bool> IsTableVisibleAsync() =>
        await _page.Locator("table.table").IsVisibleAsync();

    public async Task<string> GetHeadingTextAsync() =>
        await _page.GetByRole(AriaRole.Heading, new() { Name = "Contacts" }).InnerTextAsync();

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

    public async Task<int> GetRowCountAsync() =>
        await _page.Locator("table.table tbody tr").CountAsync();

    public async Task ClickFirstContactLinkAsync() =>
        await _page.Locator("table.table tbody tr").First
            .GetByRole(AriaRole.Link).First.ClickAsync();

    public async Task FillSearchAsync(string text) =>
        await _page.GetByPlaceholder("Search contacts...").FillAsync(text);

    public async Task<bool> HasNewContactButtonAsync() =>
        await _page.GetByRole(AriaRole.Link, new() { Name = "New Contact" }).IsVisibleAsync();

    public string CurrentUrl => _page.Url;
}
