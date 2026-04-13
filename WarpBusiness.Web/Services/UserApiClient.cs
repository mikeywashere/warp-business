using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WarpBusiness.Web.Services;

public record UserTenantInfo(Guid TenantId, string TenantName);

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    [property: JsonPropertyName("role")] string RoleName,
    DateTime CreatedAt,
    List<UserTenantInfo>? Tenants = null,
    Guid? LinkedEmployeeId = null);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    [property: JsonPropertyName("role")] string RoleName,
    Guid? TenantId = null);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    [property: JsonPropertyName("role")] string RoleName);

public record UpdateProfileRequest(
    string FirstName,
    string LastName);

/// <summary>Thrown when an API call returns a non-success status code with details.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public ApiException(int statusCode, string? responseBody)
        : base(ParseUserMessage(statusCode, responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    private static string ParseUserMessage(int statusCode, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            // Try to extract a message from JSON error responses
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? body;
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    return detail.GetString() ?? body;
                if (doc.RootElement.TryGetProperty("title", out var title))
                    return title.GetString() ?? body;
            }
            catch (System.Text.Json.JsonException)
            {
                // Not JSON — use raw body if it's short enough
                if (body.Length <= 300)
                    return body;
            }
        }

        return statusCode switch
        {
            400 => "The server rejected the request. Please check your input and try again.",
            409 => "A user with this email already exists.",
            500 => "An internal server error occurred. Please try again or contact support.",
            _ => $"Request failed with status {statusCode}."
        };
    }
}

public class UserApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<UserApiClient> _logger;

    public UserApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<UserApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken);
            _logger.LogDebug("[UserApiClient] Token applied to {Method} {Uri} (starts: {Prefix}...)",
                method, uri, _tokenProvider.AccessToken[..Math.Min(20, _tokenProvider.AccessToken.Length)]);
        }
        else
        {
            _logger.LogWarning("[UserApiClient] No token in TokenProvider for {Method} {Uri} — request will be unauthenticated", method, uri);
        }
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    public async Task<List<UserResponse>> GetUsersAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/users");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserResponse>>() ?? [];
    }

    public async Task<UserResponse?> GetUserAsync(Guid id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/users/{id}");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse?> GetCurrentUserAsync()
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "api/users/me");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserResponse>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/users", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[UserApiClient] CreateUser failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/users/{id}", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Put, "api/users/me", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/users/{id}");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }
}
