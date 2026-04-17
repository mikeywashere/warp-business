namespace WarpBusiness.Catalog.Models;

public enum PriceAdjustmentType
{
    /// <summary>No override — use Product.BasePrice</summary>
    None,
    /// <summary>BasePrice + PriceAdjustment</summary>
    Delta,
    /// <summary>BasePrice * (1 + PriceAdjustment / 100)</summary>
    Percentage
}
