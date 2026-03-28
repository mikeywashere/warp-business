namespace WarpBusiness.Plugin.Catalog.Domain;

public class ProductIngredient
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    public bool IsAllergen { get; set; }
    public string? AllergenType { get; set; }
    public int DisplayOrder { get; set; }
    public string? Notes { get; set; }
}
