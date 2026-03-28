namespace WarpBusiness.Shared.Invoicing;

public record InvoiceSettingsDto(
    Guid Id,
    string Prefix,
    int NextNumber,
    int NumberPadding,
    string DefaultPaymentTerms,
    int DefaultDueDays,
    decimal? DefaultTaxRate,
    string DefaultCurrency,
    string? DefaultFooterText,
    string? DefaultCustomerNotes,
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? CompanyLogoUrl);

public record UpdateInvoiceSettingsRequest(
    string Prefix,
    int NumberPadding,
    string DefaultPaymentTerms,
    int DefaultDueDays,
    decimal? DefaultTaxRate,
    string DefaultCurrency,
    string? DefaultFooterText,
    string? DefaultCustomerNotes,
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? CompanyLogoUrl);
