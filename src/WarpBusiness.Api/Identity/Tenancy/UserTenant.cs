namespace WarpBusiness.Api.Identity.Tenancy;

public class UserTenant
{
    public string UserId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
