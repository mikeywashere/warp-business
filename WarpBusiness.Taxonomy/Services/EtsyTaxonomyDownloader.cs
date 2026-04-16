using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class EtsyTaxonomyDownloader : ITaxonomyDownloader
{
    private const string TaxonomyUrl = "https://openapi.etsy.com/v3/application/seller-taxonomy/nodes";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EtsyTaxonomyDownloader(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public TaxonomyProvider Provider => TaxonomyProvider.Etsy;
    public bool RequiresApiKey => true;

    public async Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(out var apiKey))
            return new TaxonomyDownloadResult(false, null, null, [], "Etsy integration not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get, TaxonomyUrl);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var nodes = new List<ExternalNodeDto>();
        using var doc = JsonDocument.Parse(json);
        foreach (var rootNode in GetRootNodes(doc.RootElement))
        {
            ExtractNodes(rootNode, null, "", 0, nodes);
        }

        var checksum = ComputeSha256(JsonSerializer.Serialize(nodes));
        return new TaxonomyDownloadResult(true, null, checksum, nodes, null);
    }

    public Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    private bool IsEnabled(out string? apiKey)
    {
        apiKey = _configuration["Taxonomy:Etsy:ApiKey"];
        var enabled = _configuration.GetValue<bool>("Taxonomy:Etsy:Enabled");
        return enabled && !string.IsNullOrWhiteSpace(apiKey);
    }

    private static IEnumerable<JsonElement> GetRootNodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            return results.EnumerateArray();

        if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
            return nodes.EnumerateArray();

        return [];
    }

    private static void ExtractNodes(
        JsonElement node,
        string? parentId,
        string pathPrefix,
        int level,
        List<ExternalNodeDto> nodes)
    {
        var id = GetString(node, "id", "taxonomy_id", "node_id");
        var name = GetString(node, "name", "display_name", "category_name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return;

        var fullPath = string.IsNullOrEmpty(pathPrefix)
            ? name
            : $"{pathPrefix} > {name}";

        var children = GetChildren(node);
        var isLeaf = children.Count == 0;

        nodes.Add(new ExternalNodeDto(id, parentId, name, fullPath, level, isLeaf));

        foreach (var child in children)
        {
            ExtractNodes(child, id, fullPath, level + 1, nodes);
        }
    }

    private static List<JsonElement> GetChildren(JsonElement node)
    {
        foreach (var propertyName in new[] { "children", "child_nodes", "children_nodes", "childrenNodes" })
        {
            if (node.TryGetProperty(propertyName, out var children) && children.ValueKind == JsonValueKind.Array)
                return children.EnumerateArray().ToList();
        }

        return [];
    }

    private static string? GetString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
