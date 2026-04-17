using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;
using WarpBusiness.CommonTaxonomy.Models;

namespace WarpBusiness.CommonTaxonomy.Services;

public class TaxonomyDownloadOrchestrator
{
    private readonly IEnumerable<ITaxonomyDownloader> _downloaders;
    private readonly CommonTaxonomyDbContext _db;
    private readonly ILogger<TaxonomyDownloadOrchestrator> _logger;

    public TaxonomyDownloadOrchestrator(
        IEnumerable<ITaxonomyDownloader> downloaders,
        CommonTaxonomyDbContext db,
        ILogger<TaxonomyDownloadOrchestrator> logger)
    {
        _downloaders = downloaders;
        _db = db;
        _logger = logger;
    }

    public async Task<List<TaxonomyDownloadResult>> DownloadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<TaxonomyDownloadResult>();
        foreach (var downloader in _downloaders.Where(d => d is not IFileTaxonomyDownloader))
        {
            try
            {
                var result = await downloader.DownloadAsync(cancellationToken);
                results.Add(result);
                if (result.Success && !result.WasSkipped)
                {
                    var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Key == downloader.ProviderKey, cancellationToken);
                    if (provider is not null)
                    {
                        provider.LastDownloadedAt = DateTimeOffset.UtcNow;
                        provider.LastDownloadChecksum = result.Checksum;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Taxonomy download failed for provider {Key}", downloader.ProviderKey);
                results.Add(new TaxonomyDownloadResult { ProviderKey = downloader.ProviderKey, Success = false, ErrorMessage = ex.Message });
            }
        }
        return results;
    }
}
