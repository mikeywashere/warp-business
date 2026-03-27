using System.ComponentModel.DataAnnotations;

namespace WarpBusiness.Shared.Auth;

/// <summary>
/// Returned by GET /api/auth/my-tenants (AuthController).
/// Properties match the server-side projection from UserTenant → Tenant.
/// </summary>
public record MyTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string Role);

/// <summary>Returned by GET /api/tenants/mine (TenantsController).</summary>
public record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    string Role,
    bool IsActive = true,
    DateTimeOffset JoinedAt = default);

/// <summary>Returned by GET /api/tenants/{id} — full detail with member list.</summary>
public record TenantDetailDto(
    Guid TenantId,
    string Name,
    string Slug,
    string? DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TenantMemberDto> Members);

/// <summary>Member entry within TenantDetailDto.</summary>
public record TenantMemberDto(
    string UserId,
    string Role,
    DateTimeOffset JoinedAt,
    string? Email = null,
    string? FullName = null);

public record SelectTenantRequest([Required] Guid TenantId);

/// <summary>Matches POST /api/tenants/signup request body.</summary>
public record TenantSignupRequest(
    [Required, MaxLength(200)] string CompanyName,
    [Required, MaxLength(63), MinLength(3)] string Slug);

/// <summary>
/// Matches POST /api/tenants/signup response.
/// Includes a tenant-scoped AccessToken issued inline by TenantsController.
/// </summary>
public record TenantSignupResponse(
    Guid TenantId,
    string Name,
    string Slug,
    string Role,
    string AccessToken);

public record SlugAvailabilityResponse(bool Available);

/// <summary>Matches PUT /api/tenants/{id} request body.</summary>
public record UpdateTenantRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200)] string? DisplayName = null);

public record SamlConfigDto(
    Guid TenantId,
    string? EntityId,
    string? MetadataUrl,
    string? SsoUrl,
    string? Certificate,
    bool IsEnabled);

public record SaveSamlConfigRequest(
    [Required, MaxLength(500)] string EntityId,
    [Required, MaxLength(2000)] string MetadataUrl,
    [MaxLength(2000)] string? SsoUrl,
    string? Certificate);
