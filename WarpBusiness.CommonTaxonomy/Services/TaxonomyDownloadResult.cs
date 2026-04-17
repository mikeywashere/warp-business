namespace WarpBusiness.CommonTaxonomy.Services;

public class TaxonomyDownloadResult
{
    public bool Success { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public int NodesDownloaded { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Checksum { get; set; }
    public bool WasSkipped { get; set; } // true if checksum matched (no change)
}
