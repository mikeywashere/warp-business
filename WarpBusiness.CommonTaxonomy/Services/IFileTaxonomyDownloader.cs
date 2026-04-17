namespace WarpBusiness.CommonTaxonomy.Services;

public interface IFileTaxonomyDownloader : ITaxonomyDownloader
{
    /// <summary>Import taxonomy from an uploaded file stream</summary>
    Task<TaxonomyDownloadResult> ImportFromFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
