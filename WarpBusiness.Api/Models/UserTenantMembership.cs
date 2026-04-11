namespace WarpBusiness.Api.Models;

public class UserTenantMembership
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
