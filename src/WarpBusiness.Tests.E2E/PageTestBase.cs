using Microsoft.Playwright;
using NUnit.Framework;

namespace WarpBusiness.Tests.E2E;

/// <summary>
/// Base class for all Playwright page tests. Manages browser lifecycle and provides
/// common helpers. Extend this class in your test fixtures.
/// </summary>
public abstract class PageTestBase
{
    protected static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5002";

    protected static string TestEmail =>
        Environment.GetEnvironmentVariable("E2E_TEST_EMAIL") ?? "test@warp.local";

    protected static string TestPassword =>
        Environment.GetEnvironmentVariable("E2E_TEST_PASSWORD") ?? "Test@123!";

    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task SetUpBrowser()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    [SetUp]
    public async Task SetUpContext()
    {
        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true
        });
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(15_000);
    }

    [TearDown]
    public async Task TearDownContext()
    {
        await Context.CloseAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownBrowser()
    {
        await Browser.CloseAsync();
        Playwright.Dispose();
    }

    /// <summary>
    /// Returns true if the app is reachable at BaseUrl. Use to skip tests when the app isn't running.
    /// </summary>
    protected async Task<bool> IsAppReachableAsync()
    {
        try
        {
            var response = await Page.GotoAsync("/", new PageGotoOptions { Timeout = 5_000 });
            return response?.Ok == true || response?.Status == 200;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Skips the test with a clear message if the app is not reachable.
    /// </summary>
    protected async Task RequireAppAsync()
    {
        if (!await IsAppReachableAsync())
            Assert.Ignore($"App not reachable at {BaseUrl}. Start the Aspire host before running E2E tests.");
    }
}
