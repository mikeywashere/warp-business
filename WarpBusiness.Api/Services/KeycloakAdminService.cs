using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarpBusiness.Api.Services;

/// <summary>
/// Represents the outcome of a Keycloak Admin API operation.
/// </summary>
public record KeycloakOperationResult
{
    public bool Success { get; init; }
    public string? KeycloakUserId { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static KeycloakOperationResult Ok(string keycloakUserId) =>
        new() { Success = true, KeycloakUserId = keycloakUserId };

    public static KeycloakOperationResult Fail(HttpStatusCode statusCode, string errorMessage) =>
        new() { Success = false, StatusCode = statusCode, ErrorMessage = errorMessage };
}

public class KeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakAdminService> _logger;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private const string Realm = "warpbusiness";

    public KeycloakAdminService(HttpClient httpClient, IConfiguration configuration, ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return;

        var adminUser = _configuration["Keycloak:AdminUser"] ?? "admin";
        var adminPassword = _configuration["Keycloak:AdminPassword"] ?? "admin";

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = adminUser,
            ["password"] = adminPassword
        });

        var response = await _httpClient.PostAsync(
            "/realms/master/protocol/openid-connect/token",
            tokenRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        _accessToken = json!.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(json.ExpiresIn - 30);

        _logger.LogDebug("Obtained Keycloak admin access token.");
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequest(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return request;
    }

    public async Task<KeycloakOperationResult> CreateUserAsync(string firstName, string lastName, string email, string password, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Post, $"/admin/realms/{Realm}/users", cancellationToken);

        var userPayload = new
        {
            username = email,
            firstName,
            lastName,
            email,
            emailVerified = true,
            enabled = true,
            credentials = new[]
            {
                new { type = "password", value = password, temporary = false }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(userPayload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Keycloak user: {StatusCode} - {Error}", response.StatusCode, errorBody);

            var errorMessage = ParseKeycloakErrorMessage(errorBody) ?? "Failed to create user in identity provider.";
            return KeycloakOperationResult.Fail(response.StatusCode, errorMessage);
        }

        // Keycloak returns the new user's ID in the Location header
        var locationHeader = response.Headers.Location?.ToString();
        if (locationHeader is not null)
        {
            return KeycloakOperationResult.Ok(locationHeader.Split('/').Last());
        }

        // Fallback: look up by email
        var keycloakUser = await GetUserByEmailAsync(email, cancellationToken);
        return keycloakUser?.Id is not null
            ? KeycloakOperationResult.Ok(keycloakUser.Id)
            : KeycloakOperationResult.Fail(HttpStatusCode.InternalServerError, "User was created in Keycloak but could not be located.");
    }

    /// <summary>
    /// Parses Keycloak's JSON error response to extract a human-readable message.
    /// Keycloak returns { "errorMessage": "..." } for Admin API errors.
    /// </summary>
    private static string? ParseKeycloakErrorMessage(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("errorMessage", out var msg))
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("error_description", out var desc))
                return desc.GetString();
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString();
        }
        catch (JsonException)
        {
            // Not JSON — return null so caller uses default message
        }
        return null;
    }

    public async Task<bool> DeleteUserAsync(string keycloakUserId, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Delete, $"/admin/realms/{Realm}/users/{keycloakUserId}", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to delete Keycloak user {UserId}: {StatusCode} - {Error}", keycloakUserId, response.StatusCode, error);
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<KeycloakUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Get, $"/admin/realms/{Realm}/users?email={Uri.EscapeDataString(email)}&exact=true", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(cancellationToken);
        return users?.FirstOrDefault();
    }

    public async Task<bool> SetPasswordAsync(string keycloakUserId, string password, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Put, $"/admin/realms/{Realm}/users/{keycloakUserId}/reset-password", cancellationToken);

        var credentialPayload = new { type = "password", value = password, temporary = false };
        request.Content = new StringContent(
            JsonSerializer.Serialize(credentialPayload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to set password for Keycloak user {UserId}: {StatusCode} - {Error}", keycloakUserId, response.StatusCode, error);
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateUserAsync(string keycloakUserId, string firstName, string lastName, string email, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Put, $"/admin/realms/{Realm}/users/{keycloakUserId}", cancellationToken);

        var payload = new { firstName, lastName, email, username = email };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to update Keycloak user {UserId}: {StatusCode} - {Error}", keycloakUserId, response.StatusCode, error);
        }

        return response.IsSuccessStatusCode;
    }
}

public class KeycloakUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

internal class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
