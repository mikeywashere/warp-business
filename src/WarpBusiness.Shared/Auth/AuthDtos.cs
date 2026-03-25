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
    DateTimeOffset ExpiresAt);
