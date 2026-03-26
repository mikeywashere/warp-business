using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class ActivitiesControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public ActivitiesControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AuthenticateAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(
            client, $"acts-{Guid.NewGuid()}@example.com");
        client.SetBearerToken(token);
        return client;
    }

    [Fact]
    public async Task GetActivities_ReturnsEmptyList_WhenNoActivities()
    {
        // Arrange
        var client = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/activities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ActivityDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActivities_ReturnsActivities_ForContact()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var contact = await CreateTestContactAsync(client);
        await CreateTestActivityAsync(client, "Call with contact", contactId: contact.Id);

        // Act
        var response = await client.GetAsync($"api/activities?contactId={contact.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ActivityDto>>();
        result!.Items.Should().Contain(a => a.Subject == "Call with contact");
    }

    [Fact]
    public async Task CreateActivity_ReturnsCreated_WithValidData()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var request = new CreateActivityRequest(
            "Call", "Follow-up call", "Discuss proposal", DateTimeOffset.UtcNow.AddDays(1), null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/activities", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var activity = await response.Content.ReadFromJsonAsync<ActivityDto>();
        activity!.Subject.Should().Be("Follow-up call");
        activity.Type.Should().Be("Call");
    }

    [Fact]
    public async Task CreateActivity_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateActivityRequest(
            "Email", "Anon Activity", null, DateTimeOffset.UtcNow.AddDays(1), null, null);

        // Act
        var response = await client.PostAsJsonAsync("api/activities", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteActivity_ReturnsNoContent_WhenAuthorized()
    {
        // Arrange
        var client = await AuthenticateAsync();
        var created = await CreateTestActivityAsync(client, "Activity To Delete");

        // Act
        var response = await client.DeleteAsync($"api/activities/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<ActivityDto> CreateTestActivityAsync(
        HttpClient client, string subject, Guid? contactId = null)
    {
        var request = new CreateActivityRequest(
            "Call", subject, null, DateTimeOffset.UtcNow.AddDays(1), contactId, null);
        var response = await client.PostAsJsonAsync("api/activities", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityDto>())!;
    }

    private static async Task<ContactDto> CreateTestContactAsync(HttpClient client)
    {
        var request = new CreateContactRequest(
            "Activity", "Contact", $"actcontact-{Guid.NewGuid()}@test.com", null, null, null);
        var response = await client.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }
}
