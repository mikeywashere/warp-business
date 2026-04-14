using System.Net.Http.Headers;
using System.Text.Json;
using WarpBusiness.Cli.Models;

namespace WarpBusiness.Cli.Services;

public class WarpApiClient(string apiUrl, string accessToken)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private HttpClient CreateClient()
    {
        // Accept dev/self-signed certificates on loopback addresses
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
        var result = JsonSerializer.Deserialize<ClearResult>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return result ?? new ClearResult { Message = "Clear completed." };
    }
}

public class ClearResult
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, int>? RowsCleared { get; set; }
}
