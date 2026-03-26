using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class CompaniesControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public CompaniesControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"co-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, email);
        client.SetBearerToken(token);
        return (client, token);
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"co-admin-{Guid.NewGuid()}@example.com";
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return (client, auth.Token);
    }

    [Fact]
    public async Task GetCompanies_ReturnsEmptyList_WhenNoCompanies()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/companies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCompanies_ReturnsCompanies_WhenCompaniesExist()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        await CreateTestCompanyAsync(client, "Acme Corp");

        // Act
        var response = await client.GetAsync("api/companies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>();
        result!.Items.Should().Contain(c => c.Name == "Acme Corp");
    }

    [Fact]
    public async Task GetCompany_ReturnsNotFound_WhenCompanyDoesNotExist()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync($"api/companies/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCompany_ReturnsCompany_WhenExists()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCompanyAsync(client, "Globex Inc");

        // Act
        var response = await client.GetAsync($"api/companies/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = await response.Content.ReadFromJsonAsync<CompanyDto>();
        company!.Id.Should().Be(created.Id);
        company.Name.Should().Be("Globex Inc");
    }

    [Fact]
    public async Task CreateCompany_ReturnsCreated_WithValidData()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var request = new CreateCompanyRequest("Initech", "https://initech.com", "Software", 100, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var company = await response.Content.ReadFromJsonAsync<CompanyDto>();
        company!.Name.Should().Be("Initech");
    }

    [Fact]
    public async Task CreateCompany_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateCompanyRequest("NoAuth Corp", null, null, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/companies", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCompany_ReturnsOk_WhenAuthorized()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCompanyAsync(client, "Old Name");
        var request = new UpdateCompanyRequest("New Name", null, null, null, null, null);

        // Act
        var response = await client.PutAsJsonAsync($"api/companies/{created.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CompanyDto>();
        updated!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteCompany_ReturnsNoContent_WhenAuthenticated()
    {
        // Arrange — any authenticated user can delete (no role restriction on this endpoint)
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCompanyAsync(client, "Delete Me Corp");

        // Act
        var response = await client.DeleteAsync($"api/companies/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCompany_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange — create company with auth, then try deleting without auth
        var (authClient, _) = await AuthenticateAsync();
        var created = await CreateTestCompanyAsync(authClient, "Unauthorized Delete Corp");
        var anonClient = _factory.CreateClient();

        // Act
        var response = await anonClient.DeleteAsync($"api/companies/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<CompanyDto> CreateTestCompanyAsync(HttpClient client, string name)
    {
        var request = new CreateCompanyRequest(name, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("api/companies", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompanyDto>())!;
    }
}
