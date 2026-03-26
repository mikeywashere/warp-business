using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Plugins;

namespace WarpBusiness.Web.Services;

public class WarpApiClient(HttpClient httpClient, AuthStateService authState, NavigationManager navigationManager)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AuthStateService _authState = authState;
    private readonly NavigationManager _navigationManager = navigationManager;
    // Auth
    public async Task<AuthProviderInfo?> GetAuthProviderAsync()
        => await _httpClient.GetFromJsonAsync<AuthProviderInfo>("api/auth/provider");

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        => await _httpClient.PostAsJsonAsync("api/auth/login", request)
            .ContinueWith(t => t.Result.IsSuccessStatusCode
                ? t.Result.Content.ReadFromJsonAsync<AuthResponse>().Result
                : null);

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        => await _httpClient.PostAsJsonAsync("api/auth/register", request)
            .ContinueWith(t => t.Result.IsSuccessStatusCode
                ? t.Result.Content.ReadFromJsonAsync<AuthResponse>().Result
                : null);

    public async Task LogoutAsync()
    {
        try
        {
            await _httpClient.PostAsync("api/auth/logout", null);
        }
        catch { /* silent — clear state regardless */ }
        finally
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _authState.ClearAuth();
        }
    }

    private async Task<bool> TryRefreshAsync()
    {
        try
        {
            // Cookie is HttpOnly — browser sends it automatically
            var response = await _httpClient.PostAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth == null) return false;

            // Update the bearer token for future requests
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
            _authState.SetAuth(auth);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> request)
    {
        var response = await request();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Try to refresh — if successful, retry once
            if (await TryRefreshAsync())
            {
                response = await request();
            }
            else
            {
                // Refresh failed — session is dead
                _authState.ClearAuth();
                _navigationManager.NavigateTo("/login");
            }
        }

        return response;
    }

    // Contacts
    public async Task<PagedResult<ContactDto>?> GetContactsAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/contacts?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync(url));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>()
            : null;
    }

    public async Task<ContactDto?> GetContactAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync($"api/contacts/{id}"));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContactDto>()
            : null;
    }

    public async Task<ContactDto?> CreateContactAsync(CreateContactRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PostAsJsonAsync("api/contacts", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContactDto>()
            : null;
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, UpdateContactRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PutAsJsonAsync($"api/contacts/{id}", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContactDto>()
            : null;
    }

    public async Task DeleteContactAsync(Guid id)
        => await SendWithRefreshAsync(() => _httpClient.DeleteAsync($"api/contacts/{id}"));

    // Companies
    public async Task<PagedResult<CompanyDto>?> GetCompaniesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/companies?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync(url));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>()
            : null;
    }

    public async Task<CompanyDto?> CreateCompanyAsync(CreateCompanyRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PostAsJsonAsync("api/companies", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CompanyDto>()
            : null;
    }

    public async Task<bool> DeleteCompanyAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.DeleteAsync($"api/companies/{id}"));
        return response.IsSuccessStatusCode;
    }

    // Deals
    public async Task<PagedResult<DealDto>?> GetDealsAsync(int page = 1, int pageSize = 20)
    {
        var url = $"api/deals?page={page}&pageSize={pageSize}";
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync(url));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<DealDto>>()
            : null;
    }

    public async Task<DealDto?> GetDealAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync($"api/deals/{id}"));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DealDto>()
            : null;
    }

    public async Task<DealDto?> CreateDealAsync(CreateDealRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PostAsJsonAsync("api/deals", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DealDto>()
            : null;
    }

    public async Task<DealDto?> UpdateDealAsync(Guid id, UpdateDealRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PutAsJsonAsync($"api/deals/{id}", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DealDto>()
            : null;
    }

    public async Task<bool> DeleteDealAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.DeleteAsync($"api/deals/{id}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<DealPipelineSummary?> GetPipelineSummaryAsync()
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync("api/deals/summary"));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DealPipelineSummary>()
            : null;
    }

    // Custom Field Definitions
    public async Task<List<CustomFieldDefinitionDto>> GetCustomFieldDefinitionsAsync(string entityType = "Contact")
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync($"api/custom-fields?entityType={Uri.EscapeDataString(entityType)}"));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<CustomFieldDefinitionDto>>() ?? []
            : [];
    }

    public async Task<CustomFieldDefinitionDto?> CreateCustomFieldDefinitionAsync(CreateCustomFieldDefinitionRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PostAsJsonAsync("api/custom-fields", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>()
            : null;
    }

    public async Task<CustomFieldDefinitionDto?> UpdateCustomFieldDefinitionAsync(Guid id, UpdateCustomFieldDefinitionRequest request)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PutAsJsonAsync($"api/custom-fields/{id}", request));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>()
            : null;
    }

    public async Task<bool> DeleteCustomFieldDefinitionAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.DeleteAsync($"api/custom-fields/{id}"));
        return response.IsSuccessStatusCode;
    }

    public async Task<int> DeleteCustomFieldDefinitionRawAsync(Guid id)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.DeleteAsync($"api/custom-fields/{id}"));
        return (int)response.StatusCode;
    }

    // Admin
    public async Task<List<UserSummaryDto>?> GetUsersAsync()
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync("api/admin/users"));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>()
            : null;
    }

    public async Task<bool> SetUserRoleAsync(string userId, string role, bool add)
    {
        var response = await SendWithRefreshAsync(() => _httpClient.PostAsJsonAsync($"api/admin/users/{userId}/roles", new { role, add }));
        return response.IsSuccessStatusCode;
    }

    // Modules
    public async Task<List<ModuleInfoDto>> GetModulesAsync()
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync("api/modules"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ModuleInfoDto>>() ?? [];
    }

    public async Task<List<ModuleNavItemDto>> GetModuleNavItemsAsync()
    {
        var response = await SendWithRefreshAsync(() => _httpClient.GetAsync("api/modules/nav-items"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ModuleNavItemDto>>() ?? [];
    }
}
