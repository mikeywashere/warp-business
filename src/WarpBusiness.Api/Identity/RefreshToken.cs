using System.Security.Cryptography;
using System.Text;

namespace WarpBusiness.Api.Identity;

public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;   // SHA-256 of raw token
    public string FamilyId { get; init; } = Guid.NewGuid().ToString(); // reuse-detection family
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? DeviceHint { get; set; }  // optional: user-agent or device name

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;

    public static string GenerateRawToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
