using System.Net.Http.Json;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Web.Services;

public class WarpApiClient(HttpClient httpClient)
{
    // Auth
    public async Task<AuthProviderInfo?> GetAuthProviderAsync()
        => await httpClient.GetFromJsonAsync<AuthProviderInfo>("api/auth/provider");

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        => await httpClient.PostAsJsonAsync("api/auth/login", request)
            .ContinueWith(t => t.Result.IsSuccessStatusCode
                ? t.Result.Content.ReadFromJsonAsync<AuthResponse>().Result
                : null);

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        => await httpClient.PostAsJsonAsync("api/auth/register", request)
            .ContinueWith(t => t.Result.IsSuccessStatusCode
                ? t.Result.Content.ReadFromJsonAsync<AuthResponse>().Result
                : null);

    // Contacts
    public async Task<PagedResult<ContactDto>?> GetContactsAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/contacts?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await httpClient.GetFromJsonAsync<PagedResult<ContactDto>>(url);
    }

    public async Task<ContactDto?> GetContactAsync(Guid id)
        => await httpClient.GetFromJsonAsync<ContactDto>($"api/contacts/{id}");

    public async Task<ContactDto?> CreateContactAsync(CreateContactRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/contacts", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContactDto>()
            : null;
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, UpdateContactRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/contacts/{id}", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContactDto>()
            : null;
    }

    public async Task DeleteContactAsync(Guid id)
        => await httpClient.DeleteAsync($"api/contacts/{id}");
}
