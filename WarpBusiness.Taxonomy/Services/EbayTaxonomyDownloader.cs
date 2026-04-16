using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class EbayTaxonomyDownloader : ITaxonomyDownloader
{
    private const string TokenUrl = "https://api.ebay.com/identity/v1/oauth2/token";
    private const string TaxonomyUrl = "https://api.ebay.com/commerce/taxonomy/v1/category_tree/0";
    private const string Scope = "https://api.ebay.com/oauth/api_scope";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EbayTaxonomyDownloader(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public TaxonomyProvider Provider => TaxonomyProvider.Ebay;
    public bool RequiresApiKey => true;

    public async Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(out var clientId, out var clientSecret))
        {
            return new TaxonomyDownloadResult(false, null, null, [], "eBay integration not configured");
        }

        var token = await GetApplicationTokenAsync(clientId!, clientSecret!, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var treeResponse = await _httpClient.GetFromJsonAsync<EbayCategoryTreeResponse>(TaxonomyUrl, cancellationToken);
        if (treeResponse?.RootCategoryNode is null)
        {
            return new TaxonomyDownloadResult(false, null, null, [], "eBay taxonomy response was empty");
        }

        var nodes = FlattenTree(treeResponse.RootCategoryNode, null, "");
        var checksum = ComputeSha256(JsonSerializer.Serialize(nodes));

        return new TaxonomyDownloadResult(true, treeResponse.CategoryTreeVersion, checksum, nodes, null);
    }

    public async Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(out var clientId, out var clientSecret))
            return null;

        var token = await GetApplicationTokenAsync(clientId!, clientSecret!, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, TaxonomyUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("categoryTreeVersion", out var version)
            ? version.GetString()
            : null;
    }

    private async Task<string> GetApplicationTokenAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = Scope
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<EbayTokenResponse>(cancellationToken: cancellationToken);
        if (content?.AccessToken is null)
            throw new InvalidOperationException("Unable to acquire eBay application token.");

        return content.AccessToken;
    }

    private List<ExternalNodeDto> FlattenTree(EbayCategoryNode node, string? parentId, string pathPrefix)
    {
        var result = new List<ExternalNodeDto>();
        var fullPath = string.IsNullOrEmpty(pathPrefix)
            ? node.Category.CategoryName
            : $"{pathPrefix} > {node.Category.CategoryName}";

        var isLeaf = node.ChildCategoryTreeNodes is null || node.ChildCategoryTreeNodes.Count == 0;

        result.Add(new ExternalNodeDto(
            node.Category.CategoryId,
            parentId,
            node.Category.CategoryName,
            fullPath,
            node.CategoryTreeNodeLevel,
            isLeaf));

        if (!isLeaf)
        {
            foreach (var child in node.ChildCategoryTreeNodes!)
            {
                result.AddRange(FlattenTree(child, node.Category.CategoryId, fullPath));
            }
        }

        return result;
    }

    private bool IsEnabled(out string? clientId, out string? clientSecret)
    {
        clientId = _configuration["Taxonomy:Ebay:ClientId"];
        clientSecret = _configuration["Taxonomy:Ebay:ClientSecret"];
        var enabled = _configuration.GetValue<bool>("Taxonomy:Ebay:Enabled");
        return enabled && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class EbayTokenResponse
    {
        public string? AccessToken { get; set; }
    }

    private sealed class EbayCategoryTreeResponse
    {
        public string? CategoryTreeVersion { get; set; }
        public EbayCategoryNode RootCategoryNode { get; set; } = null!;
    }

    private sealed class EbayCategoryNode
    {
        public EbayCategory Category { get; set; } = null!;
        public List<EbayCategoryNode>? ChildCategoryTreeNodes { get; set; }
        public int CategoryTreeNodeLevel { get; set; }
    }

    private sealed class EbayCategory
    {
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}
