namespace WarpBusiness.Shared.TimeTracking;

public record CustomerBillingRateDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid CompanyId,
    string CompanyName,
    decimal HourlyRate,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record CreateCustomerBillingRateRequest(
    Guid EmployeeId,
    string EmployeeName,
    Guid CompanyId,
    string CompanyName,
    decimal HourlyRate,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes
);

public record UpdateCustomerBillingRateRequest(
    decimal HourlyRate,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes
);
