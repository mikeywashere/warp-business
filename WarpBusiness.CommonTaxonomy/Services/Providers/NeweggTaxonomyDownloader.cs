using Microsoft.Extensions.Logging;
using WarpBusiness.CommonTaxonomy.Data;

namespace WarpBusiness.CommonTaxonomy.Services.Providers;

public class NeweggTaxonomyDownloader : IFileTaxonomyDownloader
{
    public string ProviderKey => "newegg";
    private readonly CommonTaxonomyDbContext _db;
    private readonly ILogger<NeweggTaxonomyDownloader> _logger;

    public NeweggTaxonomyDownloader(CommonTaxonomyDbContext db, ILogger<NeweggTaxonomyDownloader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TaxonomyDownloadResult { ProviderKey = ProviderKey, Success = false, ErrorMessage = "Newegg taxonomy requires file import." });

    public async Task<TaxonomyDownloadResult> ImportFromFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Newegg file import not yet fully implemented");
        return new TaxonomyDownloadResult { ProviderKey = ProviderKey, Success = false, ErrorMessage = "Newegg file import parsing not yet implemented" };
    }
}
