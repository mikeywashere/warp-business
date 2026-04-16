using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record TaxonomyNodeResponse(
    Guid Id,
    Guid TenantId,
    Guid? ParentNodeId,
    string Name,
    string? Description,
    int Level,
    string MaterializedPath,
    bool IsActive,
    string? SourceProvider,
    string? SourceExternalId,
    DateTime? SourceImportedAt,
    int ChildCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateTaxonomyNodeRequest(string Name, Guid? ParentNodeId, string? Description);
public record UpdateTaxonomyNodeRequest(string? Name, string? Description, bool? IsActive);
public record MoveTaxonomyNodeRequest(Guid? NewParentId);

public record ProviderStatusResponse(
    string Provider,
    bool Enabled,
    bool RequiresApiKey,
    DateTime? LastDownload,
    int NodeCount,
    string? Version,
    string? Status);

public record ExternalTaxonomyCacheResponse(
    string Provider,
    DateTime? LastDownload,
    int NodeCount,
    string? Version,
    string? Status);

public record ExternalNodeResponse(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string FullPath,
    int Level,
    bool IsLeaf,
    bool IsImported);

public record ImportNodesRequest(string Provider, List<string> ExternalIds, Guid? TargetParentNodeId);
public record ImportResult(int NodesCreated, int NodesSkipped, List<TaxonomyNodeResponse> CreatedNodes);
public record DeleteBranchResult(bool Success, string? ErrorMessage, List<Guid>? ConflictingNodeIds);

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

    // Warp taxonomy
    public async Task<List<TaxonomyNodeResponse>> GetTaxonomyTreeAsync(CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/taxonomy");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TaxonomyNodeResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<TaxonomyNodeResponse?> GetTaxonomyNodeAsync(Guid id, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/taxonomy/{id}");
        var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaxonomyNodeResponse>(cancellationToken: ct);
    }

    public async Task<TaxonomyNodeResponse> CreateTaxonomyNodeAsync(CreateTaxonomyNodeRequest request, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/taxonomy", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "CreateTaxonomyNode", ct);
        return (await response.Content.ReadFromJsonAsync<TaxonomyNodeResponse>(cancellationToken: ct))!;
    }

    public async Task<TaxonomyNodeResponse> UpdateTaxonomyNodeAsync(Guid id, UpdateTaxonomyNodeRequest request, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/taxonomy/{id}", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "UpdateTaxonomyNode", ct);
        return (await response.Content.ReadFromJsonAsync<TaxonomyNodeResponse>(cancellationToken: ct))!;
    }

    public async Task DeleteTaxonomyNodeAsync(Guid id, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/taxonomy/{id}");
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "DeleteTaxonomyNode", ct);
    }

    public async Task MoveTaxonomyNodeAsync(Guid id, MoveTaxonomyNodeRequest request, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/taxonomy/{id}/move", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "MoveTaxonomyNode", ct);
    }

    // External providers
    public async Task<List<ProviderStatusResponse>> GetProviderStatusAsync(CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/taxonomy/external/providers");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProviderStatusResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<ExternalTaxonomyCacheResponse> TriggerDownloadAsync(string provider, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/taxonomy/external/{provider}/download");
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "TriggerDownload", ct);
        return (await response.Content.ReadFromJsonAsync<ExternalTaxonomyCacheResponse>(cancellationToken: ct))!;
    }

    public async Task<List<ExternalNodeResponse>> GetExternalTreeAsync(string provider, string? parentExternalId = null, CancellationToken ct = default)
    {
        var uri = string.IsNullOrWhiteSpace(parentExternalId)
            ? $"api/taxonomy/external/{provider}/tree"
            : $"api/taxonomy/external/{provider}/tree?parentExternalId={Uri.EscapeDataString(parentExternalId)}";
        using var request = CreateRequest(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ExternalNodeResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<List<ExternalNodeResponse>> SearchExternalNodesAsync(string provider, string query, CancellationToken ct = default)
    {
        var uri = $"api/taxonomy/external/{provider}/search?q={Uri.EscapeDataString(query)}";
        using var request = CreateRequest(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ExternalNodeResponse>>(cancellationToken: ct) ?? [];
    }

    // Import
    public async Task<ImportResult> ImportNodesAsync(ImportNodesRequest request, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/taxonomy/import", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg, ct);
        await ThrowOnErrorAsync(response, "ImportNodes", ct);
        return (await response.Content.ReadFromJsonAsync<ImportResult>(cancellationToken: ct))!;
    }

    // Nodes — roots / children / branch delete
    public async Task<List<TaxonomyNodeResponse>> GetRootNodesAsync(CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/taxonomy/nodes/roots");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TaxonomyNodeResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<List<TaxonomyNodeResponse>> GetNodeChildrenAsync(Guid nodeId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/taxonomy/nodes/{nodeId}/children");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TaxonomyNodeResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<DeleteBranchResult> DeleteBranchAsync(Guid nodeId, bool cascade = true, CancellationToken ct = default)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/taxonomy/nodes/{nodeId}?cascade={cascade.ToString().ToLower()}");
        var response = await _httpClient.SendAsync(msg, ct);

        if (response.IsSuccessStatusCode)
            return new DeleteBranchResult(true, null, null);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("[TaxonomyApiClient] DeleteBranch {NodeId} failed {Status}: {Body}", nodeId, (int)response.StatusCode, body);

        if ((int)response.StatusCode == 409)
        {
            List<Guid>? conflictIds = null;
            try { conflictIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(body); } catch { }
            // Use empty list (not null) to signal 409 even if body parse fails
            return new DeleteBranchResult(false, body, conflictIds ?? []);
        }

        return new DeleteBranchResult(false, body, null);
    }
}
