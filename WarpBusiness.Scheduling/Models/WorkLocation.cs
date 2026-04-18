namespace WarpBusiness.Scheduling.Models;

public class WorkLocation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    /// <summary>US state code (e.g. "WA", "CA") — determines which break rules apply.</summary>
    public string State { get; set; } = string.Empty;
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ScheduleTemplate> ScheduleTemplates { get; set; } = [];
    public ICollection<Schedule> Schedules { get; set; } = [];
}
