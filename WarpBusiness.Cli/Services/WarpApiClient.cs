using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WarpBusiness.Cli.Models;

namespace WarpBusiness.Cli.Services;

// ── Result records ──────────────────────────────────────────────────────────
public record TenantResult(Guid Id, string Name, string Slug);
public record EmployeeResult(Guid Id, string EmployeeNumber, string Email);
public record BusinessResult(Guid Id, string Name);
public record CustomerResult(Guid Id, string Name, string? Email);

// ── Request records (local to CLI — no project references) ──────────────────
public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    string? Department,
    string? JobTitle,
    string EmploymentStatus,
    string EmploymentType,
    decimal PayAmount,
    string PayType,
    string Currency);

public record CreateBusinessRequest(
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string Country,
    string? Notes);

public record CreateCustomerRequest(
    string Name,
    string Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string Country,
    string? Industry,
    string? CompanySize,
    string? Website,
    string? Notes,
    string Currency,
    Guid? BusinessId);

// ── Client ──────────────────────────────────────────────────────────────────
public class WarpApiClient(string apiUrl, string accessToken)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
        if (apiUrl.Contains("localhost") || apiUrl.Contains("127.0.0.1"))
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var client = new HttpClient(handler) { BaseAddress = new Uri(apiUrl) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static StringContent ToJson(object body) =>
        new(JsonSerializer.Serialize(body, WriteOptions), Encoding.UTF8, "application/json");

    public async Task<string> ExportAsync()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/admin/export", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<ClearResult> ClearAsync()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/admin/clear", null);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Clear failed ({(int)response.StatusCode}): {error}");
        }

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ClearResult>(body, ReadOptions);
        return result ?? new ClearResult { Message = "Clear completed." };
    }

    // ── Tenant ───────────────────────────────────────────────────────────────

    /// <summary>Creates a tenant. Returns null on 409 Conflict (slug/name duplicate).</summary>
    public async Task<TenantResult?> CreateTenantAsync(string name, string slug, string currencyCode = "USD")
    {
        using var client = CreateClient();
        var body = ToJson(new
        {
            name,
            slug,
            preferredCurrencyCode = currencyCode,
            loginTimeoutMinutes = 60
        });

        var response = await client.PostAsync("/api/tenants", body);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateTenant failed ({(int)response.StatusCode}): {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new TenantResult(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? name,
            root.GetProperty("slug").GetString() ?? slug);
    }

    // ── Employee ─────────────────────────────────────────────────────────────

    /// <summary>Creates an employee. Returns null on 409 Conflict (email duplicate).</summary>
    public async Task<EmployeeResult?> CreateEmployeeAsync(Guid tenantId, CreateEmployeeRequest request)
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/employees")
        {
            Content = ToJson(new
            {
                firstName = request.FirstName,
                lastName = request.LastName,
                email = request.Email,
                phone = request.Phone,
                dateOfBirth = request.DateOfBirth?.ToString("yyyy-MM-dd"),
                hireDate = request.HireDate.ToString("yyyy-MM-dd"),
                department = request.Department,
                jobTitle = request.JobTitle,
                employmentStatus = request.EmploymentStatus,
                employmentType = request.EmploymentType,
                payAmount = request.PayAmount,
                payType = request.PayType,
                currency = request.Currency
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.SendAsync(req);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateEmployee failed ({(int)response.StatusCode}): {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new EmployeeResult(
            root.GetProperty("id").GetGuid(),
            root.TryGetProperty("employeeNumber", out var en) ? en.GetString() ?? "" : "",
            request.Email);
    }

    // ── Business ─────────────────────────────────────────────────────────────

    /// <summary>Creates a business. Returns null on 409 Conflict (name duplicate).</summary>
    public async Task<BusinessResult?> CreateBusinessAsync(Guid tenantId, CreateBusinessRequest request)
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/crm/businesses")
        {
            Content = ToJson(new
            {
                name = request.Name,
                industry = request.Industry,
                website = request.Website,
                phone = request.Phone,
                address = request.Address,
                city = request.City,
                state = request.State,
                postalCode = request.PostalCode,
                country = request.Country,
                notes = request.Notes
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.SendAsync(req);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateBusiness failed ({(int)response.StatusCode}): {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new BusinessResult(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? request.Name);
    }

    // ── Customer ─────────────────────────────────────────────────────────────

    /// <summary>Creates a customer. Returns null on 409 Conflict (email duplicate).</summary>
    public async Task<CustomerResult?> CreateCustomerAsync(Guid tenantId, CreateCustomerRequest request)
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/crm/customers")
        {
            Content = ToJson(new
            {
                name = request.Name,
                email = request.Email,
                phone = request.Phone,
                address = request.Address,
                city = request.City,
                state = request.State,
                postalCode = request.PostalCode,
                country = request.Country,
                industry = request.Industry,
                companySize = request.CompanySize,
                website = request.Website,
                notes = request.Notes,
                currency = request.Currency,
                businessId = request.BusinessId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.SendAsync(req);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateCustomer failed ({(int)response.StatusCode}): {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new CustomerResult(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? request.Name,
            root.TryGetProperty("email", out var em) ? em.GetString() : null);
    }
}

public class ClearResult
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, int>? RowsCleared { get; set; }
}
