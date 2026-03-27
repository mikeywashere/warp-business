using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Identity.Tenancy;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public TenantsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
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
        var token = _tokenService.GenerateAccessToken(user, roles, tenant.Id, tenant.Slug);

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
                ut.JoinedAt))
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
            tenant.UserTenants.Select(ut => new TenantMemberDto(ut.UserId, ut.Role, ut.JoinedAt)).ToList()));
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

public record TenantSignupRequest(
    [Required, MaxLength(200)] string CompanyName,
    [Required, MaxLength(63), MinLength(3)] string Slug);

public record TenantSignupResponse(
    Guid TenantId,
    string Name,
    string Slug,
    string Role,
    string AccessToken);

public record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    string Role,
    bool IsActive,
    DateTimeOffset JoinedAt);

public record TenantDetailDto(
    Guid TenantId,
    string Name,
    string Slug,
    string? DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TenantMemberDto> Members);

public record TenantMemberDto(string UserId, string Role, DateTimeOffset JoinedAt);

public record UpdateTenantRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200)] string? DisplayName);

public record AddMemberRequest(
    [Required, EmailAddress] string Email,
    string? Role);

public record ChangeMemberRoleRequest([Required] string Role);
