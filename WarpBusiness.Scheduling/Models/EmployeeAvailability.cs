namespace WarpBusiness.Scheduling.Models;

/// <summary>
/// Recurring weekly availability rule for an employee.
/// One record per employee per day-of-week. Absence of a record means no restriction for that day.
/// </summary>
public class EmployeeAvailability
{
    public Guid Id { get; set; }

    /// <summary>Cross-context reference to employees.Employees — no EF FK.</summary>
    public Guid EmployeeId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>0 = Sunday … 6 = Saturday (DayOfWeek enum values).</summary>
    public int DayOfWeek { get; set; }

    /// <summary>False means the employee is completely unavailable this day of the week.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Earliest the employee can start. Null means no restriction.</summary>
    public TimeOnly? EarliestStartTime { get; set; }

    /// <summary>Latest the employee can work until. Null means no restriction.</summary>
    public TimeOnly? LatestEndTime { get; set; }

    public string? Notes { get; set; }
}
