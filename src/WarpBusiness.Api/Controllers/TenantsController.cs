using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Filters;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Identity.Tenancy;
using WarpBusiness.Api.Services;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ITenantSamlService _saml;

    public TenantsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ITenantSamlService saml)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
        _saml = saml;
    }

    /// <summary>POST /api/tenants/signup — self-service tenant creation</summary>
    [HttpPost("signup")]
    public async Task<ActionResult<TenantSignupResponse>> Signup(
        [FromBody] TenantSignupRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Validate slug format: lowercase alphanumeric + hyphens, 3–63 chars
        if (!IsValidSlug(request.Slug))
            return BadRequest(new { error = "Slug must be 3–63 lowercase alphanumeric characters or hyphens, and cannot start or end with a hyphen." });

        // Check slug uniqueness
        if (await _db.Tenants.AnyAsync(t => t.Slug == request.Slug, ct))
            return Conflict(new { error = "A tenant with this slug already exists." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyName,
            Slug = request.Slug,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = "TenantAdmin",
            JoinedAt = DateTimeOffset.UtcNow,
        };

        _db.Tenants.Add(tenant);
        _db.UserTenants.Add(userTenant);
        await _db.SaveChangesAsync(ct);

        // Issue tenant-scoped JWT
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateAccessToken(user, roles,
            tenant.Id, tenant.Slug, "TenantAdmin", [tenant.Id]);

        return Ok(new TenantSignupResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            "TenantAdmin",
            token));
    }

    /// <summary>GET /api/tenants/mine — list tenants the current user belongs to</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TenantSummaryDto>>> GetMine(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var tenants = await _db.UserTenants
            .Where(ut => ut.UserId == userId)
            .Include(ut => ut.Tenant)
            .AsNoTracking()
            .Select(ut => new TenantSummaryDto(
                ut.Tenant.Id,
                ut.Tenant.Name,
                ut.Tenant.Slug,
                ut.Role,
                ut.Tenant.IsActive,
                ut.JoinedAt,
                ut.Tenant.CompanyImage != null))
            .ToListAsync(ct);

        return Ok(tenants);
    }

    /// <summary>GET /api/tenants/{id} — get tenant details (TenantAdmin only)</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantDetailDto>> GetTenant(Guid id, CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var tenant = await _db.Tenants
            .Include(t => t.UserTenants)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant is null) return NotFound();

        return Ok(new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DisplayName,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.UserTenants.Select(ut => new TenantMemberDto(ut.UserId, ut.Role, ut.JoinedAt)).ToList(),
            tenant.CompanyImage != null));
    }

    /// <summary>PUT /api/tenants/{id} — update tenant (TenantAdmin only)</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantDetailDto>> UpdateTenant(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var tenant = await _db.Tenants.FindAsync([id], ct);
        if (tenant is null) return NotFound();

        tenant.Name = request.Name;
        tenant.DisplayName = request.DisplayName;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>POST /api/tenants/{id}/members — invite/add member</summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] AddMemberRequest request,
        CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var targetUser = await _userManager.FindByEmailAsync(request.Email);
        if (targetUser is null)
            return NotFound(new { error = "No user found with that email." });

        var existing = await _db.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == targetUser.Id && ut.TenantId == id, ct);

        if (existing is not null)
            return Conflict(new { error = "User is already a member of this tenant." });

        _db.UserTenants.Add(new UserTenant
        {
            UserId = targetUser.Id,
            TenantId = id,
            Role = request.Role ?? "Member",
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>DELETE /api/tenants/{id}/members/{userId} — remove member</summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var userTenant = await _db.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == id, ct);

        if (userTenant is null) return NotFound();

        _db.UserTenants.Remove(userTenant);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>PUT /api/tenants/{id}/members/{userId}/role — change member role</summary>
    [HttpPut("{id:guid}/members/{userId}/role")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid id,
        string userId,
        [FromBody] ChangeMemberRoleRequest request,
        CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var userTenant = await _db.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == id, ct);

        if (userTenant is null) return NotFound();

        userTenant.Role = request.Role;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Company Image Endpoints ─────────────────────────────────────────────

    /// <summary>PUT /api/tenants/{id}/company-image — upload company image (TenantAdmin only)</summary>
    [HttpPut("{id:guid}/company-image")]
    public async Task<IActionResult> UploadCompanyImage(
        Guid id,
        IFormFile file,
        CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        // Validate size (2MB max)
        const int maxSizeBytes = 2 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
            return BadRequest(new { error = "File size exceeds 2MB limit." });

        // Validate content type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Invalid image type. Allowed: jpeg, png, gif, webp, svg+xml." });

        var tenant = await _db.Tenants.FindAsync([id], ct);
        if (tenant is null) return NotFound();

        // Read file bytes
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        
        tenant.CompanyImage = ms.ToArray();
        tenant.CompanyImageContentType = file.ContentType;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>GET /api/tenants/{id}/company-image — download company image</summary>
    [HttpGet("{id:guid}/company-image")]
    public async Task<IActionResult> GetCompanyImage(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Check if user is a member of this tenant
        var isMember = await _db.UserTenants.AnyAsync(
            ut => ut.UserId == userId && ut.TenantId == id, ct);
        
        if (!isMember) return Forbid();

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant is null) return NotFound();

        if (tenant.CompanyImage is null || tenant.CompanyImageContentType is null)
            return NotFound(new { error = "No company image set for this tenant." });

        return File(tenant.CompanyImage, tenant.CompanyImageContentType);
    }

    /// <summary>DELETE /api/tenants/{id}/company-image — remove company image (TenantAdmin only)</summary>
    [HttpDelete("{id:guid}/company-image")]
    public async Task<IActionResult> DeleteCompanyImage(Guid id, CancellationToken ct)
    {
        if (!await IsUserInTenant(id, "TenantAdmin")) return Forbid();

        var tenant = await _db.Tenants.FindAsync([id], ct);
        if (tenant is null) return NotFound();

        tenant.CompanyImage = null;
        tenant.CompanyImageContentType = null;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── SAML Configuration Endpoints (TenantAdmin only) ─────────────────────

    /// <summary>GET /api/tenants/{tenantId}/saml — retrieve SAML config</summary>
    [HttpGet("{tenantId:guid}/saml")]
    [Authorize(Policy = "RequireTenantAdmin")]
    [RequireTenantRouteMatch]
    public async Task<ActionResult<SamlConfigDto>> GetSamlConfig(Guid tenantId, CancellationToken ct)
    {
        var config = await _saml.GetConfigAsync(tenantId, ct);
        if (config is null)
            return Ok(new SamlConfigDto(tenantId, null, null, null, null, false));

        return Ok(new SamlConfigDto(
            config.TenantId,
            config.EntityId,
            config.MetadataUrl,
            null, // SsoUrl not yet on stub entity
            null, // Certificate not yet on stub entity
            config.IsEnabled));
    }

    /// <summary>PUT /api/tenants/{tenantId}/saml — save SAML config</summary>
    [HttpPut("{tenantId:guid}/saml")]
    [Authorize(Policy = "RequireTenantAdmin")]
    [RequireTenantRouteMatch]
    public async Task<IActionResult> SaveSamlConfig(
        Guid tenantId,
        [FromBody] SaveSamlConfigRequest request,
        CancellationToken ct)
    {
        var config = new TenantSamlConfig
        {
            TenantId = tenantId,
            EntityId = request.EntityId,
            MetadataUrl = request.MetadataUrl,
        };

        await _saml.SaveConfigAsync(tenantId, config, ct);
        return NoContent();
    }

    /// <summary>POST /api/tenants/{tenantId}/saml/enable — enable SAML (validates config first)</summary>
    [HttpPost("{tenantId:guid}/saml/enable")]
    [Authorize(Policy = "RequireTenantAdmin")]
    [RequireTenantRouteMatch]
    public async Task<IActionResult> EnableSaml(Guid tenantId, CancellationToken ct)
    {
        var enabled = await _saml.EnableAsync(tenantId, ct);
        if (!enabled)
            return BadRequest(new { error = "SAML cannot be enabled: EntityId and MetadataUrl are required." });

        return NoContent();
    }

    /// <summary>POST /api/tenants/{tenantId}/saml/test — test SAML connection</summary>
    [HttpPost("{tenantId:guid}/saml/test")]
    [Authorize(Policy = "RequireTenantAdmin")]
    [RequireTenantRouteMatch]
    public async Task<IActionResult> TestSaml(Guid tenantId, CancellationToken ct)
    {
        var (success, error) = await _saml.TestConnectionAsync(tenantId, ct);
        return Ok(new { success, error });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string? GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    private async Task<bool> IsUserInTenant(Guid tenantId, string requiredRole)
    {
        var userId = GetUserId();
        if (userId is null) return false;

        return await _db.UserTenants.AnyAsync(
            ut => ut.UserId == userId && ut.TenantId == tenantId && ut.Role == requiredRole);
    }

    private static bool IsValidSlug(string slug) =>
        slug.Length is >= 3 and <= 63
        && slug[0] != '-'
        && slug[^1] != '-'
        && slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

public record AddMemberRequest(
    [Required, EmailAddress] string Email,
    string? Role);

public record ChangeMemberRoleRequest([Required] string Role);