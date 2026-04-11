using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarpBusiness.Api.Services;

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

    public async Task<string?> CreateUserAsync(string firstName, string lastName, string email, string password, CancellationToken cancellationToken = default)
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
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Keycloak user: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        // Keycloak returns the new user's ID in the Location header
        var locationHeader = response.Headers.Location?.ToString();
        if (locationHeader is not null)
        {
            return locationHeader.Split('/').Last();
        }

        // Fallback: look up by email
        var keycloakUser = await GetUserByEmailAsync(email, cancellationToken);
        return keycloakUser?.Id;
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
