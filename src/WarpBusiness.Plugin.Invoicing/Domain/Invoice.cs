namespace WarpBusiness.Plugin.Invoicing.Domain;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }

    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? PaidDate { get; set; }

    public string? PaymentTerms { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "USD";

    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountFixed { get; set; }

    public decimal? TaxRate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Notes { get; set; }
    public string? CustomerNotes { get; set; }
    public string? FooterText { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
