namespace WarpBusiness.Api.Identity.Tenancy;

public class TenantSamlConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string MetadataUrl { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
}
