namespace WarpBusiness.Plugin.Crm.Domain;

public class Activity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsCompleted => CompletedAt.HasValue;
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public Guid? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string OwnerId { get; set; } = string.Empty; // ApplicationUser.Id
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public enum ActivityType { Call, Email, Meeting, Task, Note }
