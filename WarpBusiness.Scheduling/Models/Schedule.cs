using System.Text.Json.Serialization;

namespace WarpBusiness.Scheduling.Models;

public class Schedule
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkLocationId { get; set; }
    public WorkLocation WorkLocation { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ScheduleShift> Shifts { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleStatus
{
    Draft,
    Published,
    InProgress,
    Completed,
    Archived
}
