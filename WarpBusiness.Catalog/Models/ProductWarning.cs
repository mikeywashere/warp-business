namespace WarpBusiness.Catalog.Models;

/// <summary>Junction assigning a CatalogWarning to a Product.</summary>
public class ProductWarning
{
    public Guid ProductId { get; set; }
    public Guid WarningId { get; set; }

    public Product Product { get; set; } = null!;
    public CatalogWarning Warning { get; set; } = null!;
}
