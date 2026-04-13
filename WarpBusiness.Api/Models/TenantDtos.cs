namespace WarpBusiness.Api.Models;

public record TenantResponse(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt, string? PreferredCurrencyCode, int? LoginTimeoutMinutes);
public record CreateTenantRequest(string Name, string Slug, string? PreferredCurrencyCode, int? LoginTimeoutMinutes = 480);
public record UpdateTenantRequest(string Name, string Slug, bool IsActive, string? PreferredCurrencyCode, int? LoginTimeoutMinutes = 480);
public record AddTenantMemberRequest(Guid UserId);
public record TenantMemberResponse(Guid UserId, string FirstName, string LastName, string Email, UserRole Role, DateTime JoinedAt);
public record UserTenantResponse(Guid TenantId, string TenantName, string TenantSlug);
