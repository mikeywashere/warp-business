namespace WarpBusiness.Api.Identity.Tenancy;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public TenantSamlConfig? SamlConfig { get; set; }
    public ICollection<UserTenant> UserTenants { get; set; } = [];
}
