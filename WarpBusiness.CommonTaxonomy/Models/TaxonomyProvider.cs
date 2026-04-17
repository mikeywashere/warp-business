namespace WarpBusiness.CommonTaxonomy.Models;

public class TaxonomyProvider
{
    public Guid Id { get; set; }
    /// <summary>Unique string key, e.g. "google", "amazon", "ebay", "etsy", "newegg"</summary>
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool SupportsApiDownload { get; set; }
    public bool SupportsFileImport { get; set; }
    public DateTimeOffset? LastDownloadedAt { get; set; }
    public string? LastDownloadChecksum { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TaxonomyNode> Nodes { get; set; } = [];
}
