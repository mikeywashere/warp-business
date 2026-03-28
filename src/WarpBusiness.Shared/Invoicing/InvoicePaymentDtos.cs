namespace WarpBusiness.Shared.Invoicing;

public record InvoicePaymentDto(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    DateOnly PaymentDate,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public record CreateInvoicePaymentRequest(
    decimal Amount,
    DateOnly PaymentDate,
    string PaymentMethod = "Other",
    string? ReferenceNumber = null,
    string? Notes = null);
