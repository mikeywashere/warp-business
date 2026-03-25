namespace WarpBusiness.Shared.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public record LoginRequest(
    string Email,
    string Password);

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
