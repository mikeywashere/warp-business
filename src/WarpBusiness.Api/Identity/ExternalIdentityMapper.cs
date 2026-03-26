using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace WarpBusiness.Api.Identity;

public interface IExternalIdentityMapper
{
    Task EnsureUserAsync(ClaimsPrincipal principal, AuthProviderType provider);
}

public class ExternalIdentityMapper : IExternalIdentityMapper
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalIdentityMapper> _logger;

    public ExternalIdentityMapper(
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalIdentityMapper> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task EnsureUserAsync(ClaimsPrincipal principal, AuthProviderType provider)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("preferred_username");

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("OIDC token from {Provider} has no email claim", provider);
            return;
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            existing.LastLoginAt = DateTimeOffset.UtcNow;
            // Correct AuthProvider if account was originally created locally
            if (existing.AuthProvider == "Local" || string.IsNullOrEmpty(existing.AuthProvider))
                existing.AuthProvider = provider.ToString();
            await _userManager.UpdateAsync(existing);
            return;
        }

        // First login — provision a local ApplicationUser shadow record
        var firstName = principal.FindFirstValue(ClaimTypes.GivenName)
            ?? principal.FindFirstValue("given_name")
            ?? string.Empty;
        var lastName = principal.FindFirstValue(ClaimTypes.Surname)
            ?? principal.FindFirstValue("family_name")
            ?? string.Empty;

        // Fall back: split display name if no given/family claims
        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
        {
            var name = principal.FindFirstValue("name") ?? email;
            var parts = name.Split(' ', 2);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // OIDC provider has already verified email
            AuthProvider = provider.ToString(),
            FirstName = firstName,
            LastName = lastName,
            LastLoginAt = DateTimeOffset.UtcNow,
        };

        var result = await _userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation(
                "Provisioned new user {Email} from {Provider} OIDC", email, provider);
        }
        else
        {
            _logger.LogError(
                "Failed to provision user {Email} from {Provider}: {Errors}",
                email, provider, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
