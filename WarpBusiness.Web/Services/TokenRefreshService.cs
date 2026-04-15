using System.Net.Http.Json;
using System.Text.Json;

namespace WarpBusiness.Web.Services;

public record TokenRefreshResult(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>
/// Calls Keycloak's token endpoint to exchange a refresh token for a new access token.
/// Uses a dedicated named HttpClient ("keycloak-token") to avoid circular dependencies
/// with the API client pipeline.
/// </summary>
public class TokenRefreshService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenRefreshService> _logger;

    public TokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TokenRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenRefreshResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var keycloakUrl = _configuration["services:keycloak:http:0"]
            ?? _configuration["services:keycloak:https:0"]
            ?? "http://localhost:8080";
        var tokenEndpoint = $"{keycloakUrl}/realms/warpbusiness/protocol/openid-connect/token";

        _logger.LogInformation("[TokenRefreshService] Attempting token refresh via {Endpoint}", tokenEndpoint);

        var httpClient = _httpClientFactory.CreateClient("keycloak-token");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "warpbusiness-web",
            ["refresh_token"] = refreshToken
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(form),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenRefreshService] HTTP call to Keycloak token endpoint failed");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[TokenRefreshService] Refresh failed: {StatusCode} — {Body}",
                response.StatusCode, body);
            return null;
        }

        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            var accessToken = json.GetProperty("access_token").GetString();
            var newRefreshToken = json.TryGetProperty("refresh_token", out var rtProp)
                ? rtProp.GetString()
                : null;
            var expiresIn = json.TryGetProperty("expires_in", out var expProp)
                ? expProp.GetInt32()
                : 300;

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("[TokenRefreshService] Token endpoint returned empty access_token");
                return null;
            }

            _logger.LogInformation("[TokenRefreshService] Token refreshed successfully (expires_in: {ExpiresIn}s)",
                expiresIn);

            return new TokenRefreshResult(
                accessToken,
                newRefreshToken ?? refreshToken,
                expiresIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenRefreshService] Failed to parse token endpoint response");
            return null;
        }
    }
}
