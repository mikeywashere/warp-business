using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Identity.Tenancy;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Tenancy;

/// <summary>
/// Shared fixture: two tenants, three users, tenant-scoped JWT factory.
/// Seeded once per test class via xUnit's IAsyncLifetime.
///
/// Tenant layout:
///   TenantA — UserA (TenantAdmin), SharedUser (Member)
///   TenantB — UserB (TenantAdmin), SharedUser (Member)
/// </summary>
public class MultiTenantTestFixture : IAsyncLifetime
{
    public WarpTestFactory Factory { get; private set; } = null!;

    // Stable GUIDs for the two test tenants (fixed per fixture instance)
    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();
    public string TenantASlug { get; } = "alpha-" + Guid.NewGuid().ToString("N")[..8];
    public string TenantBSlug { get; } = "beta-" + Guid.NewGuid().ToString("N")[..8];

    // User emails — unique per fixture instance to avoid cross-class pollution
    public string UserAEmail { get; } = $"user-a-{Guid.NewGuid():N}@tenancy-test.com";
    public string UserBEmail { get; } = $"user-b-{Guid.NewGuid():N}@tenancy-test.com";
    public string SharedUserEmail { get; } = $"shared-{Guid.NewGuid():N}@tenancy-test.com";

    // Tenant-scoped JWTs
    public string UserAToken { get; private set; } = string.Empty;          // UserA → TenantA
    public string UserBToken { get; private set; } = string.Empty;          // UserB → TenantB
    public string SharedUserTokenA { get; private set; } = string.Empty;    // SharedUser → TenantA
    public string SharedUserTokenB { get; private set; } = string.Empty;    // SharedUser → TenantB
    public string NoTenantToken { get; private set; } = string.Empty;       // Authenticated, no tenant_id claim
    public string InvalidTenantToken { get; private set; } = string.Empty;  // Non-existent tenant GUID
    public string WrongMemberToken { get; private set; } = string.Empty;    // UserA claiming TenantB (not a member)

    public async Task InitializeAsync()
    {
        Factory = new WarpTestFactory();
        var setupClient = Factory.CreateClient();

        // Register all three users through the public API
        await RegisterUserAsync(setupClient, UserAEmail, "Alice", "Alpha");
        await RegisterUserAsync(setupClient, UserBEmail, "Bob", "Beta");
        await RegisterUserAsync(setupClient, SharedUserEmail, "Sam", "Shared");

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Resolve user entities
        var userA = (await userManager.FindByEmailAsync(UserAEmail))!;
        var userB = (await userManager.FindByEmailAsync(UserBEmail))!;
        var sharedUser = (await userManager.FindByEmailAsync(SharedUserEmail))!;

        // Seed Tenant records directly into the in-memory ApplicationDbContext.
        // In production this happens via POST /api/tenants/signup.
        var tenantA = new Tenant
        {
            Id = TenantAId,
            Name = "Tenant Alpha",
            Slug = TenantASlug,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var tenantB = new Tenant
        {
            Id = TenantBId,
            Name = "Tenant Beta",
            Slug = TenantBSlug,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Tenants.AddRange(tenantA, tenantB);

        // UserA → TenantA (TenantAdmin)
        db.UserTenants.Add(new UserTenant
        {
            UserId = userA.Id,
            TenantId = TenantAId,
            Role = "TenantAdmin",
            JoinedAt = DateTimeOffset.UtcNow,
        });
        // UserB → TenantB (TenantAdmin)
        db.UserTenants.Add(new UserTenant
        {
            UserId = userB.Id,
            TenantId = TenantBId,
            Role = "TenantAdmin",
            JoinedAt = DateTimeOffset.UtcNow,
        });
        // SharedUser → TenantA (Member)
        db.UserTenants.Add(new UserTenant
        {
            UserId = sharedUser.Id,
            TenantId = TenantAId,
            Role = "Member",
            JoinedAt = DateTimeOffset.UtcNow,
        });
        // SharedUser → TenantB (Member)
        db.UserTenants.Add(new UserTenant
        {
            UserId = sharedUser.Id,
            TenantId = TenantBId,
            Role = "Member",
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();

        // Issue tenant-scoped JWTs via ITokenService (same key as the test host uses)
        string[] memberRoles = ["User"];
        UserAToken = tokenService.GenerateAccessToken(userA, memberRoles, TenantAId, TenantASlug);
        UserBToken = tokenService.GenerateAccessToken(userB, memberRoles, TenantBId, TenantBSlug);
        SharedUserTokenA = tokenService.GenerateAccessToken(sharedUser, memberRoles, TenantAId, TenantASlug);
        SharedUserTokenB = tokenService.GenerateAccessToken(sharedUser, memberRoles, TenantBId, TenantBSlug);

        // Edge-case tokens
        NoTenantToken = tokenService.GenerateAccessToken(userA, memberRoles, tenantId: null);
        InvalidTenantToken = tokenService.GenerateAccessToken(userA, memberRoles, Guid.NewGuid()); // tenant doesn't exist
        WrongMemberToken = tokenService.GenerateAccessToken(userA, memberRoles, TenantBId, TenantBSlug); // UserA not in TenantB
    }

    public Task DisposeAsync()
    {
        Factory?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Returns an HttpClient pre-authenticated with the given JWT token.</summary>
    public HttpClient CreateClientWithToken(string token)
    {
        var client = Factory.CreateClient();
        client.SetBearerToken(token);
        return client;
    }

    /// <summary>
    /// Returns an HttpClient with Admin + User roles, scoped to the specified tenant.
    /// Promotes the user to Admin in ASP.NET Identity, then mints a fresh token.
    /// </summary>
    public async Task<HttpClient> CreateAdminClientAsync(string userEmail, Guid tenantId, string tenantSlug)
    {
        await AuthHelper.PromoteToAdminAsync(Factory, userEmail);

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = (await userManager.FindByEmailAsync(userEmail))!;
        var token = tokenService.GenerateAccessToken(user, ["User", "Admin"], tenantId, tenantSlug);

        var client = Factory.CreateClient();
        client.SetBearerToken(token);
        return client;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static async Task RegisterUserAsync(
        HttpClient client, string email, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync(
            "api/auth/register",
            new RegisterRequest(email, "Test1234!", firstName, lastName));

        response.EnsureSuccessStatusCode();
    }
}
