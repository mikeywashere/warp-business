using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;

namespace WarpBusiness.Api.Identity.Tenancy;

/// <summary>
/// Enriches the ClaimsPrincipal with tenant membership claims after JWT validation.
/// Handles cases where tenant membership changed since the last token was issued.
/// Registered as: services.AddScoped&lt;IClaimsTransformation, TenantClaimsTransformation&gt;()
/// </summary>
public class TenantClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _db;

    public TenantClaimsTransformation(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!(principal.Identity?.IsAuthenticated ?? false))
            return principal;

        // If the token already has an active tenant_id, transformation is a no-op —
        // the issued token is authoritative for the session's active tenant.
        if (principal.HasClaim(c => c.Type == "tenant_id"))
            return principal;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
            return principal;

        // Look up all active tenant memberships for this user
        var memberships = await _db.UserTenants
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId && ut.Tenant.IsActive)
            .AsNoTracking()
            .ToListAsync();

        if (memberships.Count == 0)
            return principal;

        var identity = new ClaimsIdentity();

        // Always inject the full tenants list if missing
        if (!principal.HasClaim(c => c.Type == "tenants"))
        {
            foreach (var m in memberships)
                identity.AddClaim(new Claim("tenants", m.TenantId.ToString()));
        }

        // Auto-resolve tenant_id for single-tenant users whose token lacks the claim
        if (memberships.Count == 1 && !principal.HasClaim(c => c.Type == "tenant_id"))
        {
            var m = memberships[0];
            identity.AddClaim(new Claim("tenant_id", m.TenantId.ToString()));
            identity.AddClaim(new Claim("tenant_slug", m.Tenant.Slug));
            identity.AddClaim(new Claim("tenant_role", m.Role));
        }

        if (identity.Claims.Any())
        {
            var enriched = principal.Clone();
            ((ClaimsIdentity)enriched.Identity!).AddClaims(identity.Claims);
            return enriched;
        }

        return principal;
    }
}
