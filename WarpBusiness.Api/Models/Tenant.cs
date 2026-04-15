namespace WarpBusiness.Api.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? PreferredCurrencyCode { get; set; } // ISO 4217 3-letter code
    public int? LoginTimeoutMinutes { get; set; } = 480; // Default 8 hours
    public string? LogoBase64 { get; set; }
    public string? LogoMimeType { get; set; }
    public int? MaxUsers { get; set; }
    public string? SubscriptionPlan { get; set; }
    public string? EnabledFeatures { get; set; }

    public Currency? PreferredCurrency { get; set; }
    public ICollection<UserTenantMembership> UserMemberships { get; set; } = [];
}
