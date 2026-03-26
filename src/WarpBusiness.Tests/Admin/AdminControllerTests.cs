using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Admin;

public class AdminControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public AdminControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsUserList()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = "admin-list@example.com";
        
        // Register and promote to admin BEFORE getting token
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        
        // Login again to get a token with the Admin role claim
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);

        // Act
        var response = await client.GetAsync("api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        users.Should().NotBeNull();
        users!.Should().ContainSingle(u => u.Email == email);
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange — register as a normal user (not promoted)
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, "nonadmin@example.com");
        client.SetBearerToken(token);

        // Act
        var response = await client.GetAsync("api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange — fresh client with no auth
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetUserRole_AddAdminRole_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminEmail = "admin-setRole@example.com";
        var targetEmail = "target@example.com";
        
        // Register and promote admin, then login to get fresh token with Admin claim
        await AuthHelper.RegisterAndGetTokenAsync(client, adminEmail);
        await AuthHelper.PromoteToAdminAsync(_factory, adminEmail);
        
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(adminEmail, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        
        // Register target user
        var targetClient = _factory.CreateClient();
        await AuthHelper.RegisterAndGetTokenAsync(targetClient, targetEmail);
        
        // Get target user ID
        var usersResponse = await client.GetAsync("api/admin/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        var targetUser = users!.First(u => u.Email == targetEmail);

        // Act
        var response = await client.PostAsJsonAsync(
            $"api/admin/users/{targetUser.Id}/roles",
            new SetRoleRequest("Admin", true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SetUserRole_RemoveRole_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminEmail = "admin-removeRole@example.com";
        var targetEmail = "target-remove@example.com";
        
        // Register and promote admin, then login to get fresh token with Admin claim
        await AuthHelper.RegisterAndGetTokenAsync(client, adminEmail);
        await AuthHelper.PromoteToAdminAsync(_factory, adminEmail);
        
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(adminEmail, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        
        // Register and promote target user to admin
        var targetClient = _factory.CreateClient();
        await AuthHelper.RegisterAndGetTokenAsync(targetClient, targetEmail);
        await AuthHelper.PromoteToAdminAsync(_factory, targetEmail);
        
        // Get target user ID
        var usersResponse = await client.GetAsync("api/admin/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        var targetUser = users!.First(u => u.Email == targetEmail);

        // Act — remove Admin role
        var response = await client.PostAsJsonAsync(
            $"api/admin/users/{targetUser.Id}/roles",
            new SetRoleRequest("Admin", false));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminEmail = "admin-delete@example.com";
        var targetEmail = "delete-me@example.com";
        
        // Register and promote admin, then login to get fresh token with Admin claim
        await AuthHelper.RegisterAndGetTokenAsync(client, adminEmail);
        await AuthHelper.PromoteToAdminAsync(_factory, adminEmail);
        
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(adminEmail, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        
        // Register target user
        var targetClient = _factory.CreateClient();
        await AuthHelper.RegisterAndGetTokenAsync(targetClient, targetEmail);
        
        // Get target user ID
        var usersResponse = await client.GetAsync("api/admin/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        var targetUser = users!.First(u => u.Email == targetEmail);

        // Act
        var response = await client.DeleteAsync($"api/admin/users/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_LastAdmin_ReturnsConflict()
    {
        // Arrange — register single admin
        var client = _factory.CreateClient();
        var adminEmail = "last-admin@example.com";
        
        // Register and promote to admin, then login to get fresh token with Admin claim
        await AuthHelper.RegisterAndGetTokenAsync(client, adminEmail);
        await AuthHelper.PromoteToAdminAsync(_factory, adminEmail);
        
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(adminEmail, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        
        // Get this admin's user ID
        var usersResponse = await client.GetAsync("api/admin/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        var adminUser = users!.First(u => u.Email == adminEmail);

        // Act — attempt to delete the only admin
        var response = await client.DeleteAsync($"api/admin/users/{adminUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
