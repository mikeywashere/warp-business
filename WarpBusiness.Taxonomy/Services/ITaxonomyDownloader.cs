using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public interface ITaxonomyDownloader
{
    TaxonomyProvider Provider { get; }
    bool RequiresApiKey { get; }

    Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default);

    Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
}
