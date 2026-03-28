using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Shared.TimeTracking;

public record TimeEntryDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal Hours,
    Guid TimeEntryTypeId,
    string TimeEntryTypeName,
    bool IsBillable,
    Guid? CompanyId,
    string? CompanyName,
    decimal? BillingRate,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt
);

public record TimeEntryDetailDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal Hours,
    Guid TimeEntryTypeId,
    string TimeEntryTypeName,
    bool IsBillable,
    Guid? CompanyId,
    string? CompanyName,
    decimal? BillingRate,
    string? Description,
    string Status,
    string? ApprovedById,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy
);

public record CreateTimeEntryRequest(
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal Hours,
    Guid TimeEntryTypeId,
    bool IsBillable,
    Guid? CompanyId,
    string? CompanyName,
    decimal? BillingRate,
    string? Description
);

public record UpdateTimeEntryRequest(
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal Hours,
    Guid TimeEntryTypeId,
    bool IsBillable,
    Guid? CompanyId,
    string? CompanyName,
    decimal? BillingRate,
    string? Description
);
