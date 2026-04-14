using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// Customer DTOs
public record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Industry,
    string? CompanySize,
    string? Notes,
    string Currency,
    bool IsActive,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? BusinessId,
    string? BusinessName);

public record CreateCustomerRequest(
    string Name,
    string Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Industry,
    string? CompanySize,
    string? Notes,
    string Currency,
    Guid? BusinessId = null);

public record UpdateCustomerRequest(
    string Name,
    string Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Industry,
    string? CompanySize,
    string? Notes,
    string Currency,
    Guid? BusinessId = null);

// Business DTOs
public record BusinessResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CustomerCount);

public record CreateBusinessRequest(
    string Name,
    string? Industry = null,
    string? Website = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Notes = null);

public record UpdateBusinessRequest(
    string Name,
    string? Industry = null,
    string? Website = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Notes = null,
    bool IsActive = true);

// Customer-Employee DTOs
public record CustomerEmployeeResponse(
    Guid CustomerId,
    Guid EmployeeId,
    string Relationship,
    decimal? BillingRate,
    string? BillingCurrency,
    string EmployeeFirstName,
    string EmployeeLastName,
    string EmployeeEmail,
    DateTime CreatedAt);

public record AssignEmployeeRequest(
    Guid EmployeeId,
    string Relationship,
    decimal? BillingRate,
    string? BillingCurrency);

public record UpdateRelationshipRequest(
    string Relationship,
    decimal? BillingRate,
    string? BillingCurrency);

public class CrmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<CrmApiClient> _logger;

    public CrmApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<CrmApiClient> logger)
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
            _logger.LogDebug("[CrmApiClient] Token applied to {Method} {Uri} (starts: {Prefix}...)",
                method, uri, _tokenProvider.AccessToken[..Math.Min(20, _tokenProvider.AccessToken.Length)]);
        }
        else
        {
            _logger.LogWarning("[CrmApiClient] No token in TokenProvider for {Method} {Uri} — request will be unauthenticated", method, uri);
        }
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    // Customer CRUD operations
    public async Task<List<CustomerResponse>> GetCustomersAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/crm/customers");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CustomerResponse>>() ?? [];
    }

    public async Task<CustomerResponse?> GetCustomerAsync(Guid id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/crm/customers/{id}");
        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>();
    }

    public async Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest customerRequest)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/crm/customers", JsonContent.Create(customerRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] CreateCustomer failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    public async Task UpdateCustomerAsync(Guid id, UpdateCustomerRequest customerRequest)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/crm/customers/{id}", JsonContent.Create(customerRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] UpdateCustomer failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task DeactivateCustomerAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/crm/customers/{id}/deactivate", null);
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] DeactivateCustomer failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task ActivateCustomerAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/crm/customers/{id}/activate", null);
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] ActivateCustomer failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    // Customer-Employee relationship operations
    public async Task<List<CustomerEmployeeResponse>> GetCustomerEmployeesAsync(Guid customerId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/crm/customers/{customerId}/employees");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CustomerEmployeeResponse>>() ?? [];
    }

    public async Task AssignEmployeeAsync(Guid customerId, AssignEmployeeRequest assignRequest)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/crm/customers/{customerId}/employees", JsonContent.Create(assignRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] AssignEmployee failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task UpdateRelationshipAsync(Guid customerId, Guid employeeId, UpdateRelationshipRequest updateRequest)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/crm/customers/{customerId}/employees/{employeeId}", JsonContent.Create(updateRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] UpdateRelationship failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task UnassignEmployeeAsync(Guid customerId, Guid employeeId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/crm/customers/{customerId}/employees/{employeeId}");
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] UnassignEmployee failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task<List<EmployeeResponse>> GetAvailableEmployeesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/employees");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>() ?? [];
    }

    // Business CRUD operations
    public async Task<List<BusinessResponse>> GetBusinessesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/crm/businesses");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BusinessResponse>>() ?? [];
    }

    public async Task<BusinessResponse?> GetBusinessAsync(Guid id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/crm/businesses/{id}");
        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BusinessResponse>();
    }

    public async Task<BusinessResponse?> CreateBusinessAsync(CreateBusinessRequest businessRequest)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/crm/businesses", JsonContent.Create(businessRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] CreateBusiness failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return await response.Content.ReadFromJsonAsync<BusinessResponse>();
    }

    public async Task<BusinessResponse?> UpdateBusinessAsync(Guid id, UpdateBusinessRequest businessRequest)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/crm/businesses/{id}", JsonContent.Create(businessRequest));
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] UpdateBusiness failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return await response.Content.ReadFromJsonAsync<BusinessResponse>();
    }

    public async Task<bool> DeleteBusinessAsync(Guid id, bool unlinkCustomers = false)
    {
        var url = $"api/crm/businesses/{id}";
        if (unlinkCustomers)
            url += "?unlinkCustomers=true";

        using var msg = CreateRequest(HttpMethod.Delete, url);
        var response = await _httpClient.SendAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CrmApiClient] DeleteBusiness failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }

        return true;
    }
}
