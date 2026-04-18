namespace WarpBusiness.Scheduling.Models;

public class StaffingBlock
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public ScheduleTemplate Template { get; set; } = null!;
    public Guid PositionId { get; set; }
    /// <summary>0 = Sunday, 1 = Monday, ..., 6 = Saturday.</summary>
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int RequiredCount { get; set; }
}
