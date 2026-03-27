using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity.Tenancy;

namespace WarpBusiness.Api.Services;

// TODO: Add PackageReference to a SAML library when implementing the auth flow, e.g.:
//   <PackageReference Include="Sustainsys.Saml2.AspNetCore2" Version="x.x.x" />
// Until then, SAML config storage + management is fully implemented; auth flow is stubbed.
public class TenantSamlService : ITenantSamlService
{
    private readonly ApplicationDbContext _db;

    public TenantSamlService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(tenantId, ct);
        return config?.IsEnabled ?? false;
    }

    public async Task<TenantSamlConfig?> GetConfigAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.TenantSamlConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.TenantId == tenantId, ct);
    }

    public async Task SaveConfigAsync(Guid tenantId, TenantSamlConfig incoming, CancellationToken ct = default)
    {
        var existing = await _db.TenantSamlConfigs
            .FirstOrDefaultAsync(sc => sc.TenantId == tenantId, ct);

        if (existing is null)
        {
            incoming.Id = Guid.NewGuid();
            incoming.TenantId = tenantId;
            incoming.CreatedAt = DateTimeOffset.UtcNow;
            _db.TenantSamlConfigs.Add(incoming);
        }
        else
        {
            existing.EntityId = incoming.EntityId;
            existing.MetadataUrl = incoming.MetadataUrl;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> EnableAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await _db.TenantSamlConfigs
            .FirstOrDefaultAsync(sc => sc.TenantId == tenantId, ct);

        if (config is null)
            return false;

        if (string.IsNullOrEmpty(config.EntityId) || string.IsNullOrEmpty(config.MetadataUrl))
            return false;

        config.IsEnabled = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<(bool success, string? error)> TestConnectionAsync(Guid tenantId, CancellationToken ct = default)
    {
        // TODO: implement SAML authentication flow
        // Use Sustainsys.Saml2 (or equivalent) to attempt SP-initiated metadata fetch from MetadataUrl
        // and validate the IdP response against EntityId.
        return Task.FromResult<(bool, string?)>((false, "SAML connection test not yet implemented — library integration pending"));
    }
}
