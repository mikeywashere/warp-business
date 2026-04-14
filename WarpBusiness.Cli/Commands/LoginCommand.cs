using System.CommandLine;
using IdentityModel.Client;
using IdentityModel.OidcClient;
using WarpBusiness.Cli.Browser;
using WarpBusiness.Cli.Models;
using WarpBusiness.Cli.Services;

namespace WarpBusiness.Cli.Commands;

public static class LoginCommand
{
    private static readonly Option<string> ApiUrlOption = new(
        "--api-url",
        () => "https://localhost:7021",
        "WarpBusiness API URL");

    private static readonly Option<string> KeycloakUrlOption = new(
        "--keycloak-url",
        () => "http://localhost:8080",
        "Keycloak URL");

    public static Command Create()
    {
        var cmd = new Command("login", "Login to WarpBusiness using your Keycloak account (opens browser)");
        cmd.AddOption(ApiUrlOption);
        cmd.AddOption(KeycloakUrlOption);
        cmd.SetHandler(HandleAsync, ApiUrlOption, KeycloakUrlOption);
        return cmd;
    }

    private static async Task HandleAsync(string apiUrl, string keycloakUrl)
    {
        var port = SystemBrowser.GetRandomUnusedPort();
        var browser = new SystemBrowser(port);

        var oidcOptions = new OidcClientOptions
        {
            Authority = $"{keycloakUrl}/realms/warpbusiness",
            ClientId = "warp-cli",
            RedirectUri = browser.RedirectUri,
            Scope = "openid profile email",
            FilterClaims = false,
            Browser = browser,
            Policy = new Policy { Discovery = new DiscoveryPolicy { RequireHttps = false } }
        };

        var client = new OidcClient(oidcOptions);

        Console.WriteLine("Opening browser for login...");
        Console.WriteLine("Waiting for authentication (Ctrl+C to cancel)...");

        var result = await client.LoginAsync(new LoginRequest());

        if (result.IsError)
        {
            Console.Error.WriteLine($"❌ Login failed: {result.Error}");
            return;
        }

        var userName = result.User?.FindFirst("preferred_username")?.Value
            ?? result.User?.FindFirst("email")?.Value
            ?? "unknown";

        var token = new TokenInfo
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken ?? string.Empty,
            ExpiresAt = result.AccessTokenExpiration,
            UserName = userName,
            ApiUrl = apiUrl,
            KeycloakUrl = keycloakUrl
        };

        await TokenStore.SaveAsync(token);

        Console.WriteLine($"✅ Logged in as: {userName}");
        Console.WriteLine($"   Token valid until: {result.AccessTokenExpiration:u}");
        Console.WriteLine($"   API: {apiUrl}");
    }
}
