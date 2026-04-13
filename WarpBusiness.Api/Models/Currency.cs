namespace WarpBusiness.Api.Models;

public class Currency
{
    public string Code { get; set; } = string.Empty; // ISO 4217 3-letter code (PK)
    public string Name { get; set; } = string.Empty; // e.g. "US Dollar"
    public string? Symbol { get; set; } // e.g. "$", "€"
    public string? NumericCode { get; set; } // ISO 4217 numeric code
    public int? MinorUnit { get; set; } // decimal places (null for special currencies)
    public bool IsActive { get; set; } = true;
}
