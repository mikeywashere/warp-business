using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E.Pages;

public class DealsPage(IPage page, string baseUrl)
{
    private readonly IPage _page = page;
    private readonly string _baseUrl = baseUrl;

    public async Task GoToAsync() =>
        await _page.GotoAsync($"{_baseUrl}/deals");

    public async Task<string> GetHeadingTextAsync() =>
        await _page.GetByRole(AriaRole.Heading, new() { Name = "Deals" }).InnerTextAsync();

    public async Task<bool> HasNewDealLinkAsync() =>
        await _page.GetByRole(AriaRole.Link, new() { Name = "New Deal" }).IsVisibleAsync();

    public async Task WaitForPageReadyAsync() =>
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    public string CurrentUrl => _page.Url;
}
