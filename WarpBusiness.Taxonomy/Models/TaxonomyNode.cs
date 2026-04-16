namespace WarpBusiness.Taxonomy.Models;

public class TaxonomyNode
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid? ParentNodeId { get; set; }
    public TaxonomyNode? ParentNode { get; set; }
    public ICollection<TaxonomyNode> Children { get; set; } = [];

    public required string Name { get; set; }
    public string? Description { get; set; }
    public int Level { get; set; }
    public string MaterializedPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public string? SourceProvider { get; set; }
    public string? SourceExternalId { get; set; }
    public string? SourcePath { get; set; }
    public DateTime? SourceImportedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
