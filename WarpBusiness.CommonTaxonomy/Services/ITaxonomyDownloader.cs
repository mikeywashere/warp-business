namespace WarpBusiness.CommonTaxonomy.Services;

public interface ITaxonomyDownloader
{
    /// <summary>Unique key matching TaxonomyProvider.Key</summary>
    string ProviderKey { get; }
    /// <summary>Download full taxonomy from the provider's API</summary>
    Task<TaxonomyDownloadResult> DownloadAsync(CancellationToken cancellationToken = default);
}
