using System.ComponentModel.DataAnnotations;

namespace WarpBusiness.Shared.Auth;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(
    string Token,
    string Email,
    string FullName,
    IList<string> Roles,
    string Provider = "Local");

/// <summary>Returned by GET /api/auth/provider — tells the frontend which auth flow to use</summary>
public record AuthProviderInfo(
    string ActiveProvider,
    string? KeycloakAuthUrl,
    bool SupportsLocalLogin);

public record UserSummaryDto(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    string Provider,
    DateTimeOffset? LastLoginAt);

public record SetRoleRequest(string Role, bool Add);
