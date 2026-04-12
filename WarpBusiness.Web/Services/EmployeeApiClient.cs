using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// DTOs
public record EmployeeResponse(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    string EmploymentStatus,
    string EmploymentType,
    Guid? UserId,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    string EmploymentStatus,
    string EmploymentType,
    Guid? UserId);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    string EmploymentStatus,
    string EmploymentType,
    Guid? UserId);

public class EmployeeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<EmployeeApiClient> _logger;

    public EmployeeApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<EmployeeApiClient> logger)
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
            _logger.LogDebug("[EmployeeApiClient] Token applied to {Method} {Uri} (starts: {Prefix}...)",
                method, uri, _tokenProvider.AccessToken[..Math.Min(20, _tokenProvider.AccessToken.Length)]);
        }
        else
        {
            _logger.LogWarning("[EmployeeApiClient] No token in TokenProvider for {Method} {Uri} — request will be unauthenticated", method, uri);
        }
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    public async Task<List<EmployeeResponse>> GetEmployeesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/employees");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>() ?? [];
    }

    public async Task<EmployeeResponse?> GetEmployeeAsync(Guid id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/employees/{id}");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmployeeResponse>();
    }

    public async Task<EmployeeResponse> CreateEmployeeAsync(CreateEmployeeRequest employeeRequest)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/employees", JsonContent.Create(employeeRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[EmployeeApiClient] CreateEmployee failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>())!;
    }

    public async Task UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest employeeRequest)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/employees/{id}", JsonContent.Create(employeeRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[EmployeeApiClient] UpdateEmployee failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task DeleteEmployeeAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/employees/{id}");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }
}
