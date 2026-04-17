using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;

namespace WarpBusiness.CommonTaxonomy.Services.Providers;

public class EtsyTaxonomyDownloader : ITaxonomyDownloader
{
    public string ProviderKey => "etsy";
    private readonly CommonTaxonomyDbContext _db;
    private readonly ILogger<EtsyTaxonomyDownloader> _logger;

    public EtsyTaxonomyDownloader(CommonTaxonomyDbContext db, ILogger<EtsyTaxonomyDownloader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Etsy taxonomy download requires API credentials — not yet configured");
        return Task.FromResult(new TaxonomyDownloadResult
        {
            ProviderKey = ProviderKey,
            Success = false,
            ErrorMessage = "Etsy API credentials not configured. Configure Etsy API key in appsettings."
        });
    }
}
