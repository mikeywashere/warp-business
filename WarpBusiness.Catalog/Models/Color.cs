namespace WarpBusiness.Catalog.Models;

public class Color
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>CSS hex color code, e.g. "#FF0000"</summary>
    public string? HexCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];
}
