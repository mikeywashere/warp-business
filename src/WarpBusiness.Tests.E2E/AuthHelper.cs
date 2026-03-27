using Microsoft.Playwright;

namespace WarpBusiness.Tests.E2E;

/// <summary>
/// Shared authentication helper. Navigates to the login or register page and completes
/// the credential flow so tests start from an authenticated state.
/// </summary>
public static class AuthHelper
{
    /// <summary>
    /// Logs in as the E2E test user. If the login fails (user doesn't exist yet), registers
    /// first and then logs in. After this call, the page is at /contacts.
    /// </summary>
    public static async Task LoginAsync(IPage page, string baseUrl, string email, string password)
    {
        await page.GotoAsync($"{baseUrl}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill and submit login form
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        // Wait briefly to see if we redirected (success) or got an error (user missing)
        await page.WaitForTimeoutAsync(2_000);

        if (page.Url.Contains("/login"))
        {
            // Login failed — try registering first
            await RegisterAsync(page, baseUrl, email, password);

            // Now login
            await page.GotoAsync($"{baseUrl}/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByLabel("Email").FillAsync(email);
            await page.GetByLabel("Password").FillAsync(password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }

    /// <summary>
    /// Registers a new account via the /register page. The first name and last name are
    /// derived from the email prefix for convenience.
    /// </summary>
    public static async Task RegisterAsync(IPage page, string baseUrl, string email, string password)
    {
        await page.GotoAsync($"{baseUrl}/register");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var namePart = email.Split('@')[0];

        await page.GetByLabel("First Name").FillAsync(namePart);
        await page.GetByLabel("Last Name").FillAsync("E2E");
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Account" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
