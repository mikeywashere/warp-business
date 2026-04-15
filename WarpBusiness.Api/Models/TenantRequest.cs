namespace WarpBusiness.Api.Models;

public class TenantRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TenantRequestStatus Status { get; set; } = TenantRequestStatus.Open;
    public TenantRequestType Type { get; set; } = TenantRequestType.General;
    public string? AssignedToName { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public Tenant? Tenant { get; set; }
    public ApplicationUser? AssignedTo { get; set; }
}

public enum TenantRequestStatus
{
    Open,
    InProgress,
    Pending,
    Resolved,
    Closed,
    Cancelled
}

public enum TenantRequestType
{
    General,
    Billing,
    Technical,
    FeatureRequest,
    BugReport,
    Onboarding
}
