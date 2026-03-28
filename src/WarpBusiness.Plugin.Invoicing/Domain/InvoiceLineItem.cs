namespace WarpBusiness.Plugin.Invoicing.Domain;

public class InvoiceLineItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int LineNumber { get; set; }

    public LineItemType LineItemType { get; set; } = LineItemType.Manual;

    public string Description { get; set; } = string.Empty;

    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string? VariantDescription { get; set; }

    public Guid? TimeEntryId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateOnly? ServiceDate { get; set; }
    public decimal? Hours { get; set; }

    public decimal Quantity { get; set; } = 1;
    public string? UnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsTaxable { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
