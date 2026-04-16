using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class AmazonTaxonomyDownloader : ITaxonomyDownloader
{
    private const string Endpoint = "https://webservices.amazon.com/paapi5/getbrowsenodes";
    private const string Host = "webservices.amazon.com";
    private const string Region = "us-east-1";
    private const string Service = "ProductAdvertisingAPI";
    private const string Target = "com.amazon.paapi5.v1.ProductAdvertisingAPIv1.GetBrowseNodes";
    private const string Marketplace = "www.amazon.com";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AmazonTaxonomyDownloader> _logger;

    public AmazonTaxonomyDownloader(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AmazonTaxonomyDownloader> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string Provider => TaxonomyProvider.Amazon;
    public bool RequiresApiKey => true;

    public async Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetCredentials(out var accessKeyId, out var secretAccessKey, out var associateTag))
        {
            return new TaxonomyDownloadResult(
                false,
                null,
                null,
                [],
                "Amazon PA-API credentials not configured");
        }

        var nodes = await DownloadBrowseNodesAsync(accessKeyId, secretAccessKey, associateTag, cancellationToken);
        var checksum = ComputeSha256(JsonSerializer.Serialize(nodes));

        var parentIds = nodes
            .Where(n => n.ParentExternalId != null)
            .Select(n => n.ParentExternalId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var finalized = nodes
            .Select(n => n with { IsLeaf = !parentIds.Contains(n.ExternalId) })
            .ToList();

        return new TaxonomyDownloadResult(true, null, checksum, finalized, null);
    }

    public Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    private async Task<List<ExternalNodeDto>> DownloadBrowseNodesAsync(
        string accessKeyId,
        string secretAccessKey,
        string associateTag,
        CancellationToken ct)
    {
        var nodes = new Dictionary<string, ExternalNodeDto>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        pending.Enqueue("0");

        while (pending.Count > 0)
        {
            var batch = new List<string>();
            while (pending.Count > 0 && batch.Count < 10)
            {
                var id = pending.Dequeue();
                if (visited.Add(id))
                    batch.Add(id);
            }

            if (batch.Count == 0)
                continue;

            var responseJson = await SendBrowseNodesRequestAsync(batch, accessKeyId, secretAccessKey, associateTag, ct);
            AppendNodesFromResponse(responseJson, nodes, pending);
        }

        return nodes.Values.ToList();
    }

    private async Task<string> SendBrowseNodesRequestAsync(
        IReadOnlyList<string> browseNodeIds,
        string accessKeyId,
        string secretAccessKey,
        string associateTag,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            BrowseNodeIds = browseNodeIds,
            PartnerTag = associateTag,
            PartnerType = "Associates",
            Marketplace,
            Resources = new[]
            {
                "BrowseNodes.Id",
                "BrowseNodes.DisplayName",
                "BrowseNodes.Ancestor",
                "BrowseNodes.Children",
                "BrowseNodes.IsCategoryRoot"
            }
        });

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("content-encoding", "amz-1.0");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-target", Target);
        request.Headers.Host = Host;

        var authorization = BuildAuthorizationHeader(
            accessKeyId,
            secretAccessKey,
            amzDate,
            dateStamp,
            payload);

        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Amazon PA-API request failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Amazon PA-API request failed: {response.StatusCode}");
        }

        return body;
    }

    private static string BuildAuthorizationHeader(
        string accessKeyId,
        string secretAccessKey,
        string amzDate,
        string dateStamp,
        string payload)
    {
        var canonicalUri = "/paapi5/getbrowsenodes";
        var canonicalQueryString = "";
        var canonicalHeaders =
            $"content-encoding:amz-1.0\n" +
            $"content-type:application/json; charset=utf-8\n" +
            $"host:{Host}\n" +
            $"x-amz-date:{amzDate}\n" +
            $"x-amz-target:{Target}\n";
        var signedHeaders = "content-encoding;content-type;host;x-amz-date;x-amz-target";
        var payloadHash = HashHex(payload);

        var canonicalRequest = string.Join("\n",
            "POST",
            canonicalUri,
            canonicalQueryString,
            canonicalHeaders,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var stringToSign = string.Join("\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            HashHex(canonicalRequest));

        var signingKey = GetSignatureKey(secretAccessKey, dateStamp, Region, Service);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        return $"AWS4-HMAC-SHA256 Credential={accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static void AppendNodesFromResponse(
        string responseJson,
        Dictionary<string, ExternalNodeDto> nodes,
        Queue<string> pending)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("Errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            var message = errors[0].TryGetProperty("Message", out var messageProp)
                ? messageProp.GetString()
                : "Amazon PA-API error";
            throw new InvalidOperationException(message);
        }

        if (!root.TryGetProperty("BrowseNodesResult", out var result) ||
            !result.TryGetProperty("BrowseNodes", out var browseNodes) ||
            browseNodes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var node in browseNodes.EnumerateArray())
        {
            var id = node.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
            var name = node.TryGetProperty("DisplayName", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                continue;

            var ancestorChain = GetAncestorChain(node);
            AddAncestorNodes(ancestorChain, nodes);

            var parentId = ancestorChain.Count > 0 ? ancestorChain[^1].Id : null;
            var fullPath = BuildFullPath(ancestorChain.Select(a => a.Name), name);
            var level = ancestorChain.Count;
            AddNode(nodes, id, parentId, name, fullPath, level);

            if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    var childId = child.TryGetProperty("Id", out var childIdProp)
                        ? childIdProp.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(childId))
                        pending.Enqueue(childId);

                    var childName = child.TryGetProperty("DisplayName", out var childNameProp)
                        ? childNameProp.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(childId) && !string.IsNullOrWhiteSpace(childName))
                    {
                        var childPath = $"{fullPath} > {childName}";
                        AddNode(nodes, childId!, id, childName!, childPath, level + 1);
                    }
                }
            }
        }
    }

    private static List<AmazonAncestor> GetAncestorChain(JsonElement node)
    {
        var chain = new List<AmazonAncestor>();
        if (node.TryGetProperty("Ancestor", out var ancestor) && ancestor.ValueKind == JsonValueKind.Object)
            AppendAncestorChain(ancestor, chain);
        return chain;
    }

    private static void AppendAncestorChain(JsonElement ancestor, List<AmazonAncestor> chain)
    {
        if (ancestor.TryGetProperty("Ancestor", out var parent) && parent.ValueKind == JsonValueKind.Object)
            AppendAncestorChain(parent, chain);

        var id = ancestor.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
        var name = ancestor.TryGetProperty("DisplayName", out var nameProp) ? nameProp.GetString() : null;
        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            chain.Add(new AmazonAncestor(id!, name!));
    }

    private static void AddAncestorNodes(
        IReadOnlyList<AmazonAncestor> ancestors,
        Dictionary<string, ExternalNodeDto> nodes)
    {
        string? parentId = null;
        var pathSegments = new List<string>();
        for (var i = 0; i < ancestors.Count; i++)
        {
            var ancestor = ancestors[i];
            pathSegments.Add(ancestor.Name);
            var fullPath = string.Join(" > ", pathSegments);
            AddNode(nodes, ancestor.Id, parentId, ancestor.Name, fullPath, i);
            parentId = ancestor.Id;
        }
    }

    private static void AddNode(
        Dictionary<string, ExternalNodeDto> nodes,
        string id,
        string? parentId,
        string name,
        string fullPath,
        int level)
    {
        if (nodes.ContainsKey(id))
            return;

        nodes[id] = new ExternalNodeDto(id, parentId, name, fullPath, level, false);
    }

    private static string BuildFullPath(IEnumerable<string> ancestors, string name)
    {
        var parts = ancestors.Concat(new[] { name });
        return string.Join(" > ", parts);
    }

    private bool TryGetCredentials(out string accessKeyId, out string secretAccessKey, out string associateTag)
    {
        accessKeyId = _configuration["Taxonomy:Amazon:AccessKeyId"] ?? string.Empty;
        secretAccessKey = _configuration["Taxonomy:Amazon:SecretAccessKey"] ?? string.Empty;
        associateTag = _configuration["Taxonomy:Amazon:AssociateTag"] ?? string.Empty;

        return !string.IsNullOrWhiteSpace(accessKeyId)
            && !string.IsNullOrWhiteSpace(secretAccessKey)
            && !string.IsNullOrWhiteSpace(associateTag);
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string HashHex(string data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ToHex(hash);
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static byte[] GetSignatureKey(string secretKey, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{secretKey}"), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static string ToHex(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();

    private sealed record AmazonAncestor(string Id, string Name);
}
