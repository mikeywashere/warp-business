using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WarpBusiness.Taxonomy.Data;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class TaxonomyDownloadService
{
    private readonly TaxonomyDbContext _db;
    private readonly IEnumerable<ITaxonomyDownloader> _downloaders;
    private readonly ILogger<TaxonomyDownloadService> _logger;

    public TaxonomyDownloadService(
        TaxonomyDbContext db,
        IEnumerable<ITaxonomyDownloader> downloaders,
        ILogger<TaxonomyDownloadService> logger)
    {
        _db = db;
        _downloaders = downloaders;
        _logger = logger;
    }

    public async Task<ExternalTaxonomyCache> DownloadAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var downloader = _downloaders.FirstOrDefault(d => string.Equals(d.Provider, provider, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No downloader registered for {provider}.");

        var cache = new ExternalTaxonomyCache
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            DownloadedAt = DateTime.UtcNow,
            Status = ExternalCacheStatus.Downloading
        };

        _db.ExternalTaxonomyCaches.Add(cache);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await downloader.DownloadAsync(cancellationToken);
            if (!result.Success)
            {
                cache.Status = ExternalCacheStatus.Failed;
                cache.ErrorMessage = result.ErrorMessage;
                await _db.SaveChangesAsync(cancellationToken);
                return cache;
            }

            await _db.ExternalTaxonomyNodes
                .Where(n => n.Provider == provider)
                .ExecuteDeleteAsync(cancellationToken);

            var nodes = result.Nodes.Select(dto => new ExternalTaxonomyNode
            {
                Id = Guid.NewGuid(),
                CacheId = cache.Id,
                Provider = provider,
                ExternalId = dto.ExternalId,
                ParentExternalId = dto.ParentExternalId,
                Name = dto.Name,
                FullPath = dto.FullPath,
                Level = dto.Level,
                IsLeaf = dto.IsLeaf
            }).ToList();

            _db.ExternalTaxonomyNodes.AddRange(nodes);

            cache.NodeCount = nodes.Count;
            cache.SourceVersion = result.Version;
            cache.SourceChecksum = result.Checksum;
            cache.Status = ExternalCacheStatus.Completed;

            await _db.SaveChangesAsync(cancellationToken);
            return cache;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Taxonomy download failed for {Provider}", provider);
            cache.Status = ExternalCacheStatus.Failed;
            cache.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
