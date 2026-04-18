namespace WarpBusiness.Scheduling.Models;

public class ScheduleTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkLocationId { get; set; }
    public WorkLocation WorkLocation { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StaffingBlock> StaffingBlocks { get; set; } = [];
}
