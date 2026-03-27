using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class DealsControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public DealsControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AuthenticateAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(
            client, $"deals-{Guid.NewGuid()}@example.com");
        client.SetBearerToken(token);
        return client;
    }

    private async Task<HttpClient> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"deals-admin-{Guid.NewGuid()}@example.com";
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return client;
    }

    [Fact]
    public async Task GetDeals_ReturnsEmptyList_WhenNoDeals()
    {
        // Arrange
        var client = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/deals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DealDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeals_ReturnsDeals_WhenDealsExist()
    {
        // Arrange
        var client = await AuthenticateAsync();
        await CreateTestDealAsync(client, "Big Deal");

        // Act
        var response = await client.GetAsync("api/deals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DealDto>>();
        result!.Items.Should().Contain(d => d.Title == "Big Deal");
    }

    [Fact]
    public async Task GetDeal_ReturnsNotFound_WhenDealDoesNotExist()
    {
        // Arrange
        var client = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync($"api/deals/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateDeal_ReturnsCreated_WithValidData()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var request = new CreateDealRequest(
            "New Deal", 50000m, "USD", "Prospecting", 25, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/deals", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var deal = await response.Content.ReadFromJsonAsync<DealDto>();
        deal!.Title.Should().Be("New Deal");
        deal.Value.Should().Be(50000m);
        deal.Stage.Should().Be("Prospecting");
    }

    [Fact]
    public async Task CreateDeal_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateDealRequest(
            "Anon Deal", 1000m, "USD", "Prospecting", 10, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/deals", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDeal_ReturnsOk_WhenAuthorized()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var created = await CreateTestDealAsync(client, "Old Deal Title");
        var request = new UpdateDealRequest(
            "Updated Deal Title", 75000m, "USD", "Proposal", 50, null, null, null);

        // Act
        var response = await client.PutAsJsonAsync($"api/deals/{created.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<DealDto>();
        updated!.Title.Should().Be("Updated Deal Title");
        updated.Stage.Should().Be("Proposal");
    }

    [Fact]
    public async Task DeleteDeal_ReturnsNoContent_WhenAuthorized()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var created = await CreateTestDealAsync(client, "Deal To Delete");

        // Act
        var response = await client.DeleteAsync($"api/deals/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteDeal_ReturnsNoContent_WhenAdmin()
    {
        // Arrange
        var adminClient = await AuthenticateAsAdminAsync();
        var created = await CreateTestDealAsync(adminClient, "Deal To Delete");

        // Act
        var response = await adminClient.DeleteAsync($"api/deals/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteDeal_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange — create with admin, attempt delete with regular user
        var adminClient = await AuthenticateAsAdminAsync();
        var created = await CreateTestDealAsync(adminClient, "Forbidden Deal Delete");
        var userClient = await AuthenticateAsync();

        // Act
        var response = await userClient.DeleteAsync($"api/deals/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<DealDto> CreateTestDealAsync(HttpClient client, string title)
    {
        var request = new CreateDealRequest(title, 10000m, "USD", "Prospecting", 20, null, null, null);
        var response = await client.PostAsJsonAsync("api/deals", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DealDto>())!;
    }
}
