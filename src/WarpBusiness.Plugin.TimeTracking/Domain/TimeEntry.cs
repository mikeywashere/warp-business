namespace WarpBusiness.Plugin.TimeTracking.Domain;

public class TimeEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public decimal Hours { get; set; }
    public Guid TimeEntryTypeId { get; set; }
    public TimeEntryType? TimeEntryType { get; set; }
    public bool IsBillable { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public decimal? BillingRate { get; set; }
    public string? Description { get; set; }
    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;
    public string? ApprovedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public enum TimeEntryStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected
}
