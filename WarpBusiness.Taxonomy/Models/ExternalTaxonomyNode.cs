namespace WarpBusiness.Taxonomy.Models;

public class ExternalTaxonomyNode
{
    public Guid Id { get; set; }
    public Guid CacheId { get; set; }
    public ExternalTaxonomyCache Cache { get; set; } = null!;

    public TaxonomyProvider Provider { get; set; }
    public required string ExternalId { get; set; }
    public string? ParentExternalId { get; set; }

    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
}
