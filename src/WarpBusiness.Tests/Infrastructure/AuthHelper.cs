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

        // 2. Signup for a new tenant — returns a tenant-scoped token required by
        //    the RequireActiveTenant policy on CRM and Employee Management endpoints.
        client.SetBearerToken(auth!.Token);
        var slug = "test-" + Guid.NewGuid().ToString("N")[..10];
        var signupResponse = await client.PostAsJsonAsync("api/tenants/signup",
            new { CompanyName = $"{firstName}'s Test Company", Slug = slug });

        if (signupResponse.IsSuccessStatusCode)
        {
            var signup = await signupResponse.Content.ReadFromJsonAsync<WarpBusiness.Api.Controllers.TenantSignupResponse>();
            return signup!.AccessToken;
        }

        // Fallback: return the basic token if signup fails (should not happen in normal test flow)
        return auth.Token;
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
