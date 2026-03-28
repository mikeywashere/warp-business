using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WarpBusiness.Api.Identity;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Tests.Infrastructure;

public static class AuthHelper
{
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email = "test@example.com",
        string password = "Test1234!",
        string firstName = "Test",
        string lastName = "User")
    {
        // 1. Register the user — returns a basic (no tenant) token
        var registerResponse = await client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, password, firstName, lastName));
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // 2. The registration token doesn't include tenant claims yet.
        //    We'll create a tenant directly in the database and mint a tenant-scoped token.
        //    This bypasses POST /api/tenants/signup to avoid authentication complications in tests.
        
        // Return the basic token for now - tenant will be added separately if needed
        return auth!.Token;
    }

    public static async Task<string> RegisterAndGetTenantTokenAsync(
        WarpTestFactory factory,
        HttpClient client,
        string email = "test@example.com",
        string password = "Test1234!",
        string firstName = "Test",
        string lastName = "User")
    {
        // 1. Register the user via HTTP
        var registerResponse = await client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, password, firstName, lastName));
        registerResponse.EnsureSuccessStatusCode();

        // 2. Create tenant and membership directly in the database (like MultiTenantTestFixture does)
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarpBusiness.Api.Data.ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<WarpBusiness.Api.Identity.ITokenService>();

        var user = await userManager.FindByEmailAsync(email);
        if (user == null) throw new InvalidOperationException($"User {email} not found after registration");

        var tenantId = Guid.NewGuid();
        var slug = "test-" + Guid.NewGuid().ToString("N")[..10];
        
        var tenant = new WarpBusiness.Api.Identity.Tenancy.Tenant
        {
            Id = tenantId,
            Name = $"{firstName}'s Test Company",
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var userTenant = new WarpBusiness.Api.Identity.Tenancy.UserTenant
        {
            UserId = user.Id,
            TenantId = tenantId,
            Role = "TenantAdmin",
            JoinedAt = DateTimeOffset.UtcNow,
        };

        db.Tenants.Add(tenant);
        db.UserTenants.Add(userTenant);
        await db.SaveChangesAsync();

        // 3. Generate tenant-scoped token
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.GenerateAccessToken(user, roles, tenantId, slug, "TenantAdmin", [tenantId]);

        return token;
    }

    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task PromoteToAdminAsync(WarpTestFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        
        // Ensure Admin role exists
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
            await userManager.AddToRoleAsync(user, "Admin");
    }
}
