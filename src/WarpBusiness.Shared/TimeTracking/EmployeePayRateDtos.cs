namespace WarpBusiness.Shared.TimeTracking;

public record EmployeePayRateDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    decimal Rate,
    string RateType,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record CreateEmployeePayRateRequest(
    Guid EmployeeId,
    string EmployeeName,
    decimal Rate,
    string RateType,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes
);

public record UpdateEmployeePayRateRequest(
    decimal Rate,
    string RateType,
    string Currency,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string? Notes
);
