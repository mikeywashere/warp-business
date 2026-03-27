using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Identity.Tenancy;

public class JwtTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("tenant_id")?.Value;
            return claim != null ? Guid.Parse(claim) : Guid.Empty;
        }
    }

    public string TenantSlug =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant_slug")?.Value ?? string.Empty;

    public bool IsResolved => TenantId != Guid.Empty;
}
