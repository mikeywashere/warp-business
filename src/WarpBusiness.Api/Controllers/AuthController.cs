using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "User");
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Issue refresh token
        var (rawRefresh, _) = await _tokenService.CreateRefreshTokenAsync(
            user.Id, Request.Headers.UserAgent);
        Response.Cookies.Append("warp_refresh", rawRefresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new AuthResponse(
            token,
            user.Email!,
            user.FullName,
            roles.ToList(),
            "Local"));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized();

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized();

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        // Resolve tenant memberships to embed in the token
        var memberships = await _db.UserTenants
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == user.Id && ut.Tenant.IsActive)
            .AsNoTracking()
            .ToListAsync();

        string token;
        Guid? activeTenantId = null;

        if (memberships.Count == 1)
        {
            // Single tenant: issue full token with tenant claims
            var m = memberships[0];
            activeTenantId = m.TenantId;
            token = _tokenService.GenerateAccessToken(user, roles,
                m.TenantId, m.Tenant.Slug, m.Role,
                [m.TenantId]);
        }
        else if (memberships.Count > 1)
        {
            // Multiple tenants: issue pre-auth token — client must call /select-tenant
            token = _tokenService.GeneratePreAuthToken(user, memberships.Select(m => m.TenantId).ToList());
        }
        else
        {
            // No tenants yet: basic token
            token = _tokenService.GenerateAccessToken(user, roles);
        }

        var (rawRefresh, _) = await _tokenService.CreateRefreshTokenAsync(
            user.Id, Request.Headers.UserAgent, activeTenantId: activeTenantId);
        Response.Cookies.Append("warp_refresh", rawRefresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new AuthResponse(
            token,
            user.Email!,
            user.FullName,
            roles.ToList(),
            "Local"));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new AuthResponse(
            string.Empty,
            user.Email!,
            user.FullName,
            roles.ToList(),
            "Local"));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies["warp_refresh"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(new { error = "No refresh token" });

        var result = await _tokenService.ValidateAndRotateRefreshTokenAsync(rawToken);
        if (!result.IsValid || result.UserId == null)
            return Unauthorized(new { error = result.Error ?? "Invalid refresh token" });

        var user = await _userManager.FindByIdAsync(result.UserId);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        string accessToken;

        if (result.ActiveTenantId.HasValue && result.ActiveTenantId != Guid.Empty)
        {
            // Re-issue with the same active tenant
            var ut = await _db.UserTenants
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.TenantId == result.ActiveTenantId.Value);

            if (ut is not null)
            {
                var allIds = await _db.UserTenants
                    .Where(x => x.UserId == user.Id && x.Tenant.IsActive)
                    .Select(x => x.TenantId)
                    .ToListAsync();

                accessToken = _tokenService.GenerateAccessToken(user, roles,
                    ut.TenantId, ut.Tenant.Slug, ut.Role, allIds);
            }
            else
            {
                accessToken = _tokenService.GenerateAccessToken(user, roles);
            }
        }
        else
        {
            accessToken = _tokenService.GenerateAccessToken(user, roles);
        }

        Response.Cookies.Append("warp_refresh", result.NewRawToken!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new AuthResponse(
            accessToken,
            user.Email!,
            user.FullName,
            roles.ToList(),
            "Local"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
            await _tokenService.RevokeUserRefreshTokensAsync(userId);

        Response.Cookies.Delete("warp_refresh");
        return NoContent();
    }

    /// <summary>
    /// POST /api/auth/select-tenant
    /// Validates the user is a member of the requested tenant, then issues a new JWT
    /// with tenant_id / tenant_slug / tenant_role / tenants claims set.
    /// Used by the tenant picker UI after login when a user belongs to multiple tenants.
    /// </summary>
    [HttpPost("select-tenant")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> SelectTenant([FromBody] SelectTenantRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var userTenant = await _db.UserTenants
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == request.TenantId);

        if (userTenant is null)
            return Forbid();

        if (!userTenant.Tenant.IsActive)
            return BadRequest(new { error = "Tenant is not active." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        var allTenantIds = await _db.UserTenants
            .Where(ut => ut.UserId == userId && ut.Tenant.IsActive)
            .Select(ut => ut.TenantId)
            .ToListAsync();

        var token = _tokenService.GenerateAccessToken(user, roles,
            userTenant.TenantId,
            userTenant.Tenant.Slug,
            userTenant.Role,
            allTenantIds);

        // Update the refresh token's active tenant so future refreshes carry the selection
        await _tokenService.RevokeUserRefreshTokensAsync(userId);
        var (rawRefresh, _) = await _tokenService.CreateRefreshTokenAsync(
            userId, Request.Headers.UserAgent, activeTenantId: request.TenantId);
        Response.Cookies.Append("warp_refresh", rawRefresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new AuthResponse(token, user.Email!, user.FullName, roles.ToList(), "Local"));
    }

    /// <summary>
    /// GET /api/auth/my-tenants
    /// Returns all tenants the current user belongs to.
    /// Used to populate the tenant picker UI.
    /// </summary>
    [HttpGet("my-tenants")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MyTenantDto>>> GetMyTenants()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var tenants = await _db.UserTenants
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId && ut.Tenant.IsActive)
            .AsNoTracking()
            .Select(ut => new MyTenantDto(
                ut.Tenant.Id,
                ut.Tenant.Name,
                ut.Tenant.Slug,
                ut.Role,
                ut.Tenant.CompanyImage != null))
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("provider")]
    [AllowAnonymous]
    public IActionResult GetProvider([FromServices] IOptions<AuthProviderOptions> options)
    {
        var active = options.Value.ActiveProvider;
        var keycloak = options.Value.Keycloak;

        return Ok(new AuthProviderInfo(
            ActiveProvider: active.ToString(),
            KeycloakAuthUrl: active == AuthProviderType.Keycloak && !string.IsNullOrEmpty(keycloak.Authority)
                ? $"{keycloak.Authority}/protocol/openid-connect/auth"
                : null,
            SupportsLocalLogin: active == AuthProviderType.Local));
    }
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

/// <summary>Request body for POST /api/auth/select-tenant.</summary>
public record SelectTenantRequest(Guid TenantId);
