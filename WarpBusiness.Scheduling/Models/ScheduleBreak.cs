using System.Text.Json.Serialization;

namespace WarpBusiness.Scheduling.Models;

public class ScheduleBreak
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public ScheduleShift Shift { get; set; } = null!;
    public BreakType BreakType { get; set; }
    public bool IsPaid { get; set; }
    public TimeOnly? ScheduledStartTime { get; set; }
    public TimeOnly? ScheduledEndTime { get; set; }
    public TimeOnly? ActualStartTime { get; set; }
    public TimeOnly? ActualEndTime { get; set; }
    /// <summary>Whether this break was actually taken (for compliance tracking).</summary>
    public bool WasTaken { get; set; } = false;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BreakType
{
    Rest,
    Meal
}
