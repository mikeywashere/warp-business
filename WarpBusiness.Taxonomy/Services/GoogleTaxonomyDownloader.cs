using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class GoogleTaxonomyDownloader : ITaxonomyDownloader
{
    private const string TaxonomyUrl =
        "https://www.google.com/basepages/producttype/taxonomy-with-ids.en-US.txt";

    private static readonly Regex VersionRegex = new(@"Version:\s*(.+)", RegexOptions.Compiled);
    private readonly HttpClient _httpClient;

    public GoogleTaxonomyDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public TaxonomyProvider Provider => TaxonomyProvider.Google;
    public bool RequiresApiKey => false;

    public async Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync(TaxonomyUrl, cancellationToken);
        var checksum = ComputeSha256(response);

        var nodes = new List<ExternalNodeDto>();
        var pathToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? version = null;

        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith('#'))
            {
                var match = VersionRegex.Match(trimmed);
                if (match.Success)
                    version = match.Groups[1].Value.Trim();
                continue;
            }

            var dashIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
            if (dashIndex < 0)
                continue;

            var id = trimmed[..dashIndex].Trim();
            var fullPath = trimmed[(dashIndex + 3)..].Trim();
            var segments = fullPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            var name = segments[^1].Trim();
            var level = segments.Length - 1;

            var parentPath = level > 0
                ? string.Join(" > ", segments[..^1])
                : null;

            var parentId = parentPath != null && pathToId.TryGetValue(parentPath, out var pid)
                ? pid
                : null;

            pathToId[fullPath] = id;
            nodes.Add(new ExternalNodeDto(id, parentId, name, fullPath, level, false));
        }

        var parentIds = nodes
            .Where(n => n.ParentExternalId != null)
            .Select(n => n.ParentExternalId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var finalized = nodes
            .Select(n => n with { IsLeaf = !parentIds.Contains(n.ExternalId) })
            .ToList();

        return new TaxonomyDownloadResult(true, version, checksum, finalized, null);
    }

    public async Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync(TaxonomyUrl, cancellationToken);
        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('#'))
                continue;

            var match = VersionRegex.Match(trimmed);
            if (match.Success)
                return match.Groups[1].Value.Trim();
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
