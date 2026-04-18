using System.Text.Json.Serialization;

namespace WarpBusiness.Scheduling.Models;

public class ScheduleShift
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;
    /// <summary>References employees.Employees.Id — no cross-context EF navigation.</summary>
    public Guid EmployeeId { get; set; }
    public Guid PositionId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly ScheduledStartTime { get; set; }
    public TimeOnly ScheduledEndTime { get; set; }
    public TimeOnly? ActualStartTime { get; set; }
    public TimeOnly? ActualEndTime { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ScheduleBreak> Breaks { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftStatus
{
    Scheduled,
    Confirmed,
    InProgress,
    Completed,
    NoShow,
    Absent,
    Cancelled
}
