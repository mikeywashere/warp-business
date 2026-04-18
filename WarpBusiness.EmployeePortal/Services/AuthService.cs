using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace WarpBusiness.EmployeePortal.Services;

public class AuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IHttpContextAccessor httpContextAccessor, ILogger<AuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid GetTenantIdFromJwt()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("[AuthService] User not authenticated");
            return Guid.Empty;
        }

        var subClaim = httpContext.User.FindFirst("sub");
        if (subClaim is null || !Guid.TryParse(subClaim.Value, out var tenantId))
        {
            _logger.LogWarning("[AuthService] Failed to extract ID from 'sub' claim");
            return Guid.Empty;
        }

        return tenantId;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }
}
