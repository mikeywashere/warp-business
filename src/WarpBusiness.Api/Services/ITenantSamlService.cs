using WarpBusiness.Api.Identity.Tenancy;

namespace WarpBusiness.Api.Services;

public interface ITenantSamlService
{
    Task<bool> IsEnabledAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSamlConfig?> GetConfigAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveConfigAsync(Guid tenantId, TenantSamlConfig config, CancellationToken ct = default);
    /// <summary>Enables SAML for a tenant. Returns false if required config is missing.</summary>
    Task<bool> EnableAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Tests the SAML connection. Returns (true, null) on success or (false, errorMessage) on failure.</summary>
    Task<(bool success, string? error)> TestConnectionAsync(Guid tenantId, CancellationToken ct = default);
}
