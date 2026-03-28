namespace WarpBusiness.Plugin.Invoicing.Domain;

public class InvoiceSettings
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Prefix { get; set; } = "INV-";
    public int NextNumber { get; set; } = 1;
    public int NumberPadding { get; set; } = 5;

    public string DefaultPaymentTerms { get; set; } = "Net 30";
    public int DefaultDueDays { get; set; } = 30;
    public decimal? DefaultTaxRate { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public string? DefaultFooterText { get; set; }
    public string? DefaultCustomerNotes { get; set; }

    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyLogoUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
