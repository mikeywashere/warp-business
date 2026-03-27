using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class ContactsControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;
    private readonly HttpClient _client;

    public ContactsControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AuthenticateAsync()
    {
        var token = await AuthHelper.RegisterAndGetTokenAsync(
            _client, $"contacts-{Guid.NewGuid()}@example.com");
        _client.SetBearerToken(token);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"contacts-admin-{Guid.NewGuid()}@example.com";
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return client;
    }

    [Fact]
    public async Task GetContacts_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("api/contacts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetContacts_Authenticated_ReturnsPagedResult()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("api/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task CreateContact_WithValidData_ReturnsCreated()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new CreateContactRequest(
            "Alice", "Smith", "alice@example.com", "+1555000001", "Engineer", null);

        // Act
        var response = await _client.PostAsJsonAsync("api/contacts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var contact = await response.Content.ReadFromJsonAsync<ContactDto>();
        contact!.FirstName.Should().Be("Alice");
        contact.LastName.Should().Be("Smith");
        contact.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetContactById_ExistingContact_ReturnsContact()
    {
        // Arrange
        await AuthenticateAsync();
        var created = await CreateTestContactAsync("Bob", "Jones");

        // Act
        var response = await _client.GetAsync($"api/contacts/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await response.Content.ReadFromJsonAsync<ContactDto>();
        contact!.Id.Should().Be(created.Id);
        contact.FullName.Should().Be("Bob Jones");
    }

    [Fact]
    public async Task GetContactById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync($"api/contacts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateContact_ExistingContact_ReturnsUpdated()
    {
        // Arrange — Admin can update any contact (IDOR protection: non-admins can only update own contact)
        var adminClient = await CreateAdminClientAsync();
        var created = await CreateTestContactWithClientAsync(adminClient, "Charlie", "Brown");
        var updateRequest = new UpdateContactRequest(
            "Charles", "Brown", "charles@example.com", null, "Manager", null, "Active");

        // Act
        var response = await adminClient.PutAsJsonAsync($"api/contacts/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ContactDto>();
        updated!.FirstName.Should().Be("Charles");
        updated.Email.Should().Be("charles@example.com");
    }

    [Fact]
    public async Task DeleteContact_ExistingContact_ReturnsNoContent_WhenAdmin()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();

        var created = await CreateTestContactWithClientAsync(adminClient, "Delete", "Me");

        // Act
        var response = await adminClient.DeleteAsync($"api/contacts/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteContact_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange — create with admin, attempt delete with regular user
        var adminClient = await CreateAdminClientAsync();

        var created = await CreateTestContactWithClientAsync(adminClient, "Forbidden", "Delete");
        await AuthenticateAsync();

        // Act
        var response = await _client.DeleteAsync($"api/contacts/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteContact_NonExistentId_ReturnsNotFound_WhenAdmin()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();

        // Act
        var response = await adminClient.DeleteAsync($"api/contacts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetContacts_WithSearch_ReturnsFilteredResults()
    {
        // Arrange
        await AuthenticateAsync();
        await CreateTestContactAsync("Searchable", "Person");

        // Act
        var response = await _client.GetAsync("api/contacts?search=Searchable");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>();
        result!.Items.Should().Contain(c => c.FirstName == "Searchable");
    }

    private async Task<ContactDto> CreateTestContactAsync(
        string firstName, string lastName) =>
        await CreateTestContactAsync(_client, firstName, lastName);

    private static async Task<ContactDto> CreateTestContactAsync(
        HttpClient client, string firstName, string lastName)
    {
        var request = new CreateContactRequest(
            firstName, lastName,
            $"{firstName.ToLower()}.{lastName.ToLower()}-{Guid.NewGuid():N}@test.com",
            null, null, null);
        var response = await client.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }

    private static async Task<ContactDto> CreateTestContactWithClientAsync(
        HttpClient client, string firstName, string lastName)
    {
        var request = new CreateContactRequest(
            firstName, lastName,
            $"{firstName.ToLower()}.{lastName.ToLower()}-{Guid.NewGuid():N}@test.com",
            null, null, null);
        var response = await client.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }
}
