using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Plugin.EmployeeManagement.Controllers;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.EmployeeManagement;

public class EmployeesControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public EmployeesControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AuthenticateAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(
            client, $"emp-user-{Guid.NewGuid()}@example.com");
        client.SetBearerToken(token);
        return client;
    }

    private async Task<HttpClient> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"emp-admin-{Guid.NewGuid()}@example.com";
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return client;
    }

    [Fact]
    public async Task GetEmployees_ReturnsEmptyList_WhenNoEmployees()
    {
        // Arrange
        var client = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        result.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetEmployees_ReturnsEmployees_WhenEmployeesExist()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        await CreateTestEmployeeAsync(adminClient, "John", "Doe");

        // Act
        var response = await adminClient.GetAsync("api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        result.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
    {
        // Arrange
        var client = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync($"api/employees/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEmployee_ReturnsCreated_WithValidData()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var request = new EmployeeRequest(
            "Jane", "Smith", $"jane-{Guid.NewGuid()}@company.com",
            null, "Engineering", "Software Engineer",
            DateOnly.FromDateTime(DateTime.Today), null, true, null, null);

        // Act
        var response = await adminClient.PostAsJsonAsync("api/employees", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        employee.GetProperty("firstName").GetString().Should().Be("Jane");
        employee.GetProperty("lastName").GetString().Should().Be("Smith");
    }

    [Fact]
    public async Task CreateEmployee_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new EmployeeRequest(
            "Anon", "User", "anon@company.com",
            null, null, null,
            DateOnly.FromDateTime(DateTime.Today), null, true, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/employees", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEmployee_ReturnsForbidden_WhenNotAdminOrManager()
    {
        // Arrange — regular user (not Admin or Manager)
        var client = await AuthenticateAsync();
        var request = new EmployeeRequest(
            "Forbidden", "User", $"forbidden-{Guid.NewGuid()}@company.com",
            null, null, null,
            DateOnly.FromDateTime(DateTime.Today), null, true, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/employees", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateEmployee_ReturnsNoContent_WhenAdmin()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var id = await CreateTestEmployeeAsync(adminClient, "Active", "Worker");

        // Act
        var response = await adminClient.PostAsync($"api/employees/{id}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeactivateEmployee_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var id = await CreateTestEmployeeAsync(adminClient, "Persist", "Worker");
        var userClient = await AuthenticateAsync();

        // Act
        var response = await userClient.PostAsync($"api/employees/{id}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEmployee_ReturnsNoContent_WhenAdmin()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var id = await CreateTestEmployeeAsync(adminClient, "Delete", "Me");

        // Act
        var response = await adminClient.DeleteAsync($"api/employees/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteEmployee_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var id = await CreateTestEmployeeAsync(adminClient, "Stay", "Put");
        var userClient = await AuthenticateAsync();

        // Act
        var response = await userClient.DeleteAsync($"api/employees/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CreateTestEmployeeAsync(
        HttpClient client, string firstName, string lastName)
    {
        var request = new EmployeeRequest(
            firstName, lastName, $"{firstName.ToLower()}-{Guid.NewGuid()}@company.com",
            null, "Engineering", "Tester",
            DateOnly.FromDateTime(DateTime.Today), null, true, null, null);
        var response = await client.PostAsJsonAsync("api/employees", request);
        response.EnsureSuccessStatusCode();
        var employee = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return employee.GetProperty("id").GetGuid();
    }
}
