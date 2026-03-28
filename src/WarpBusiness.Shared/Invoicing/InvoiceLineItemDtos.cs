namespace WarpBusiness.Shared.Invoicing;

public record InvoiceLineItemDto(
    Guid Id,
    int LineNumber,
    string LineItemType,
    string Description,
    Guid? ProductId,
    string? ProductName,
    string? ProductSku,
    Guid? ProductVariantId,
    string? VariantDescription,
    Guid? TimeEntryId,
    Guid? EmployeeId,
    string? EmployeeName,
    DateOnly? ServiceDate,
    decimal? Hours,
    decimal Quantity,
    string? UnitOfMeasure,
    decimal UnitPrice,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    decimal LineTotal,
    bool IsTaxable);

public record CreateInvoiceLineItemRequest(
    string LineItemType = "Manual",
    string Description = "",
    Guid? ProductId = null,
    string? ProductName = null,
    string? ProductSku = null,
    Guid? ProductVariantId = null,
    string? VariantDescription = null,
    Guid? TimeEntryId = null,
    Guid? EmployeeId = null,
    string? EmployeeName = null,
    DateOnly? ServiceDate = null,
    decimal? Hours = null,
    decimal Quantity = 1,
    string? UnitOfMeasure = null,
    decimal UnitPrice = 0,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    bool IsTaxable = true);

public record UpdateInvoiceLineItemRequest(
    string Description,
    decimal Quantity,
    string? UnitOfMeasure,
    decimal UnitPrice,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    bool IsTaxable);
