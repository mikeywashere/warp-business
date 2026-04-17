using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;

namespace WarpBusiness.CommonTaxonomy.Services.Providers;

public class EbayTaxonomyDownloader : ITaxonomyDownloader
{
    public string ProviderKey => "ebay";
    private readonly CommonTaxonomyDbContext _db;
    private readonly ILogger<EbayTaxonomyDownloader> _logger;

    public EbayTaxonomyDownloader(CommonTaxonomyDbContext db, ILogger<EbayTaxonomyDownloader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("eBay taxonomy download requires API credentials — not yet configured");
        return Task.FromResult(new TaxonomyDownloadResult
        {
            ProviderKey = ProviderKey,
            Success = false,
            ErrorMessage = "eBay API credentials not configured. Configure eBay OAuth credentials in appsettings."
        });
    }
}
