using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ProviderStatusResponse(
    Guid Id,
    string Key,
    string DisplayName,
    bool SupportsApiDownload,
    bool SupportsFileImport,
    DateTimeOffset? LastDownloadedAt,
    string? LastDownloadChecksum,
    bool IsActive,
    int NodeCount);

public record DownloadResult(
    bool Success,
    string ProviderKey,
    int NodesDownloaded,
    string? ErrorMessage,
    string? Checksum,
    bool WasSkipped);

public record ExternalNodeResponse(
    Guid Id,
    string ExternalId,
    string Name,
    string FullPath,
    Guid? ParentId,
    int Depth,
    bool IsLeaf);

public record ProviderNodesResponse(
    int Page,
    int PageSize,
    int TotalCount,
    List<ExternalNodeResponse> Nodes);

// ── Client ────────────────────────────────────────────────────────────────────

public class TaxonomyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<TaxonomyApiClient> _logger;

    public TaxonomyApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<TaxonomyApiClient> logger)
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
        }
        else
        {
            _logger.LogWarning("[TaxonomyApiClient] No token in TokenProvider for {Method} {Uri}", method, uri);
        }

        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);

        return request;
    }

    private async Task ThrowOnErrorAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("[TaxonomyApiClient] {Operation} failed with {StatusCode}: {Body}",
                operation, (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    public async Task<List<ProviderStatusResponse>> GetProviderStatusAsync(CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/taxonomy/providers");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProviderStatusResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<DownloadResult> TriggerDownloadAsync(string provider, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/taxonomy/providers/{provider}/download");
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "TriggerDownload", ct);
        return (await response.Content.ReadFromJsonAsync<DownloadResult>(cancellationToken: ct))!;
    }

    public async Task<ProviderNodesResponse> GetExternalNodesAsync(string provider, Guid? parentId = null, int page = 1, int pageSize = 200, CancellationToken ct = default)
    {
        var uri = $"api/taxonomy/providers/{provider}/nodes?page={page}&pageSize={pageSize}";
        if (parentId.HasValue)
            uri += $"&parentId={parentId.Value}";
        using var request = CreateRequest(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProviderNodesResponse>(cancellationToken: ct))!;
    }

    public async Task<List<ExternalNodeResponse>> SearchExternalNodesAsync(string provider, string query, CancellationToken ct = default)
    {
        var uri = $"api/taxonomy/providers/{provider}/nodes/search?q={Uri.EscapeDataString(query)}";
        using var request = CreateRequest(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ExternalNodeResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<List<ExternalNodeResponse>> GetNodeChildrenAsync(Guid nodeId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/taxonomy/nodes/{nodeId}/children");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ExternalNodeResponse>>(cancellationToken: ct) ?? [];
    }
}
