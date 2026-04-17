using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;
using WarpBusiness.CommonTaxonomy.Models;

namespace WarpBusiness.CommonTaxonomy.Services.Providers;

public class GoogleTaxonomyDownloader : ITaxonomyDownloader
{
    public string ProviderKey => "google";

    private readonly CommonTaxonomyDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<GoogleTaxonomyDownloader> _logger;

    private const string TaxonomyUrl = "https://www.google.com/basepages/producttype/taxonomy-with-ids.en-US.txt";

    public GoogleTaxonomyDownloader(CommonTaxonomyDbContext db, HttpClient http, ILogger<GoogleTaxonomyDownloader> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Key == ProviderKey, cancellationToken);
        if (provider is null)
            return new TaxonomyDownloadResult { ProviderKey = ProviderKey, Success = false, ErrorMessage = "Provider not found in database" };

        string content;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, TaxonomyUrl);
            // Google blocks requests without a recognisable User-Agent
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; WarpBusiness/1.0)");
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            content = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new TaxonomyDownloadResult { ProviderKey = ProviderKey, Success = false, ErrorMessage = ex.Message };
        }

        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        if (provider.LastDownloadChecksum == checksum)
            return new TaxonomyDownloadResult { ProviderKey = ProviderKey, Success = true, WasSkipped = true, Checksum = checksum };

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith('#') && l.Contains('-'))
            .ToList();

        var existingNodes = await _db.Nodes.Where(n => n.ProviderId == provider.Id).ToListAsync(cancellationToken);
        _db.Nodes.RemoveRange(existingNodes);
        await _db.SaveChangesAsync(cancellationToken);

        var pathToId = new Dictionary<string, Guid>();
        var nodesToAdd = new List<TaxonomyNode>();

        foreach (var line in lines)
        {
            var dashIdx = line.IndexOf(" - ");
            if (dashIdx < 0) continue;
            var externalId = line[..dashIdx].Trim();
            var fullPath = line[(dashIdx + 3)..].Trim();
            var parts = fullPath.Split(" > ");
            var name = parts[^1];

            Guid? parentId = null;
            if (parts.Length > 1)
            {
                var parentPath = string.Join(" > ", parts[..^1]);
                parentId = pathToId.TryGetValue(parentPath, out var pid) ? pid : (Guid?)null;
            }

            var id = Guid.NewGuid();
            pathToId[fullPath] = id;

            nodesToAdd.Add(new TaxonomyNode
            {
                Id = id,
                ProviderId = provider.Id,
                ExternalId = externalId,
                FullPath = fullPath,
                Name = name,
                ParentId = parentId,
                Depth = parts.Length - 1,
                IsLeaf = true
            });
        }

        var leafIds = nodesToAdd.Select(n => n.Id).ToHashSet();
        foreach (var node in nodesToAdd.Where(n => n.ParentId.HasValue))
            leafIds.Remove(node.ParentId!.Value);
        foreach (var node in nodesToAdd)
            node.IsLeaf = leafIds.Contains(node.Id);

        const int chunkSize = 500;
        for (int i = 0; i < nodesToAdd.Count; i += chunkSize)
        {
            _db.Nodes.AddRange(nodesToAdd.Skip(i).Take(chunkSize));
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Google taxonomy: imported {Count} nodes", nodesToAdd.Count);

        return new TaxonomyDownloadResult
        {
            ProviderKey = ProviderKey,
            Success = true,
            NodesDownloaded = nodesToAdd.Count,
            Checksum = checksum
        };
    }
}
