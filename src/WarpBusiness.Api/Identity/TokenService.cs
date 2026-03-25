using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WarpBusiness.Api.Data;

namespace WarpBusiness.Api.Identity;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    Task<(string rawRefreshToken, RefreshToken entity)> CreateRefreshTokenAsync(
        string userId, string? deviceHint = null, string? familyId = null);
    Task<RefreshTokenValidationResult> ValidateAndRotateRefreshTokenAsync(string rawToken);
    Task RevokeUserRefreshTokensAsync(string userId);
}

public record RefreshTokenValidationResult(
    bool IsValid,
    string? UserId,
    RefreshToken? NewToken,
    string? NewRawToken,
    string? Error);

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _db;

    public TokenService(IConfiguration config, ApplicationDbContext db)
    {
        _config = config;
        _db = db;
    }

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var expiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string rawRefreshToken, RefreshToken entity)> CreateRefreshTokenAsync(
        string userId, string? deviceHint = null, string? familyId = null)
    {
        var rawToken = RefreshToken.GenerateRawToken();
        var tokenHash = RefreshToken.HashToken(rawToken);
        var expiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId ?? Guid.NewGuid().ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays),
            DeviceHint = deviceHint
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return (rawToken, refreshToken);
    }

    public async Task<RefreshTokenValidationResult> ValidateAndRotateRefreshTokenAsync(string rawToken)
    {
        var tokenHash = RefreshToken.HashToken(rawToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null)
            return new RefreshTokenValidationResult(false, null, null, null, "Token not found");

        // Reuse attack detection: token was already used (revoked and replaced)
        if (token.IsRevoked && token.ReplacedByTokenHash != null)
        {
            // Revoke entire family
            var familyTokens = await _db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var familyToken in familyTokens)
                familyToken.RevokedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            return new RefreshTokenValidationResult(false, null, null, null, "Token reuse detected - family revoked");
        }

        if (token.IsExpired)
            return new RefreshTokenValidationResult(false, null, null, null, "Token expired");

        if (token.IsRevoked)
            return new RefreshTokenValidationResult(false, null, null, null, "Token revoked");

        // Valid token - rotate it
        var (newRawToken, newToken) = await CreateRefreshTokenAsync(token.UserId, token.DeviceHint, token.FamilyId);
        
        // Mark old token as revoked and replaced
        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ReplacedByTokenHash = newToken.TokenHash;

        await _db.SaveChangesAsync();

        return new RefreshTokenValidationResult(true, token.UserId, newToken, newRawToken, null);
    }

    public async Task RevokeUserRefreshTokensAsync(string userId)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
            token.RevokedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }
}
