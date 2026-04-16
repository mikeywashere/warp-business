namespace WarpBusiness.Taxonomy.Models;

public class ExternalTaxonomyCache
{
    public Guid Id { get; set; }
    public TaxonomyProvider Provider { get; set; }
    public DateTime DownloadedAt { get; set; }
    public int NodeCount { get; set; }
    public string? SourceVersion { get; set; }
    public string? SourceChecksum { get; set; }
    public ExternalCacheStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
