namespace WarpBusiness.Scheduling.Models;

/// <summary>
/// Records which positions an employee is qualified to work.
/// EmployeeId is a cross-context reference to employees.Employees — no EF navigation.
/// </summary>
public class EmployeePosition
{
    public Guid EmployeeId { get; set; }
    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public Guid TenantId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
