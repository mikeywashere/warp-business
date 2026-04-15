using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace WarpBusiness.TenantPortal.Services;

public class TokenRefreshService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenRefreshService> _logger;

    public TokenRefreshService(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TokenRefreshService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> RefreshTokenAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("[TokenRefresh] No HttpContext, cannot refresh");
            return false;
        }

        var refreshToken = await httpContext.GetTokenAsync("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("[TokenRefresh] No refresh_token found");
            return false;
        }

        var keycloakUrl = _configuration["services:keycloak:http:0"]
            ?? _configuration["services:keycloak:https:0"]
            ?? "http://localhost:8080";

        var tokenEndpoint = $"{keycloakUrl}/realms/warpbusiness/protocol/openid-connect/token";

        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", "warpbusiness-tenant-portal"),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

        try
        {
            var client = _httpClientFactory.CreateClient("keycloak-token");
            var response = await client.PostAsync(tokenEndpoint, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[TokenRefresh] Keycloak returned {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

            var newAccessToken = tokenResponse.GetProperty("access_token").GetString();
            var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString();
            var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

            var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult?.Principal is null)
            {
                _logger.LogWarning("[TokenRefresh] No authenticated principal found");
                return false;
            }

            authResult.Properties!.UpdateTokenValue("access_token", newAccessToken);
            authResult.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
            authResult.Properties.UpdateTokenValue("expires_at",
                DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("o"));

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                authResult.Principal, authResult.Properties);

            _logger.LogInformation("[TokenRefresh] Successfully refreshed tokens");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenRefresh] Exception during token refresh");
            return false;
        }
    }
}
