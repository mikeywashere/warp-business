using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;

namespace WarpBusiness.CommonTaxonomy.Services.Providers;

public class AmazonTaxonomyDownloader : IFileTaxonomyDownloader
{
    public string ProviderKey => "amazon";
    private readonly CommonTaxonomyDbContext _db;
    private readonly ILogger<AmazonTaxonomyDownloader> _logger;

    public AmazonTaxonomyDownloader(CommonTaxonomyDbContext db, ILogger<AmazonTaxonomyDownloader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TaxonomyDownloadResult
        {
            ProviderKey = ProviderKey,
            Success = false,
            ErrorMessage = "Amazon taxonomy requires file import. Upload a Browse Tree Guide file via the API."
        });

    public async Task<TaxonomyDownloadResult> ImportFromFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Amazon file import not yet fully implemented");
        return new TaxonomyDownloadResult
        {
            ProviderKey = ProviderKey,
            Success = false,
            ErrorMessage = "Amazon file import parsing not yet implemented"
        };
    }
}
