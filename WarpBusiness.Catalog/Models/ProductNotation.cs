namespace WarpBusiness.Catalog.Models;

/// <summary>Junction assigning a CatalogNotation to a Product.</summary>
public class ProductNotation
{
    public Guid ProductId { get; set; }
    public Guid NotationId { get; set; }

    public Product Product { get; set; } = null!;
    public CatalogNotation Notation { get; set; } = null!;
}
