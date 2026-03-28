using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class ContactEmployeeRelationshipsControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public ContactEmployeeRelationshipsControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"rel-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        return (client, token);
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"rel-admin-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return (client, auth.Token);
    }

    // ── Relationship Types ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelationshipTypes_ReturnsEmptyList_WhenNoneExist()
    {
        var (client, _) = await AuthenticateAsync();

        var response = await client.GetAsync("api/contact-relationship-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<ContactEmployeeRelationshipTypeDto>>();
        types.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRelationshipType_RequiresAdmin()
    {
        var (client, _) = await AuthenticateAsync();
        var request = new CreateContactEmployeeRelationshipTypeRequest("Sales");

        var response = await client.PostAsJsonAsync("api/contact-relationship-types", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateRelationshipType_ReturnsCreated_WhenAdmin()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var request = new CreateContactEmployeeRelationshipTypeRequest(
            "Service Technician", "Field service and repairs", DisplayOrder: 1);

        var response = await client.PostAsJsonAsync("api/contact-relationship-types", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var type = await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipTypeDto>();
        type!.Name.Should().Be("Service Technician");
        type.Description.Should().Be("Field service and repairs");
        type.DisplayOrder.Should().Be(1);
        type.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRelationshipType_RequiresAdmin()
    {
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTypeAsync(adminClient, "To Update");

        var (userClient, _) = await AuthenticateAsync();
        var update = new UpdateContactEmployeeRelationshipTypeRequest("Updated", DisplayOrder: 5);

        var response = await userClient.PutAsJsonAsync($"api/contact-relationship-types/{created.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRelationshipType_ReturnsOk_WhenAdmin()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTypeAsync(client, "Old Name");
        var update = new UpdateContactEmployeeRelationshipTypeRequest("New Name", "Updated desc", 10, false);

        var response = await client.PutAsJsonAsync($"api/contact-relationship-types/{created.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipTypeDto>();
        updated!.Name.Should().Be("New Name");
        updated.Description.Should().Be("Updated desc");
        updated.DisplayOrder.Should().Be(10);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRelationshipType_RequiresAdmin()
    {
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTypeAsync(adminClient, "To Delete");

        var (userClient, _) = await AuthenticateAsync();
        var response = await userClient.DeleteAsync($"api/contact-relationship-types/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRelationshipType_ReturnsNoContent_WhenAdmin()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTypeAsync(client, "Delete Me");

        var response = await client.DeleteAsync($"api/contact-relationship-types/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteRelationshipType_ReturnsConflict_WhenInUse()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var type = await CreateTypeAsync(client, "In Use Type");
        var contact = await CreateTestContactAsync(client);

        // Create a relationship using this type
        var relRequest = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Jane Doe", "jane@example.com", type.Id);
        var relResponse = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", relRequest);
        relResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Try to delete the type
        var response = await client.DeleteAsync($"api/contact-relationship-types/{type.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteRelationshipType_ReturnsNotFound_WhenDoesNotExist()
    {
        var (client, _) = await AuthenticateAsAdminAsync();

        var response = await client.DeleteAsync($"api/contact-relationship-types/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Relationships ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelationships_ReturnsEmptyList_WhenNoneExist()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var contact = await CreateTestContactAsync(client);

        var response = await client.GetAsync($"api/contacts/{contact.Id}/relationships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var relationships = await response.Content.ReadFromJsonAsync<List<ContactEmployeeRelationshipDto>>();
        relationships.Should().NotBeNull();
        relationships.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRelationship_ReturnsCreated()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var type = await CreateTypeAsync(client, "Sales Rep");
        var contact = await CreateTestContactAsync(client);

        var request = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Bob Smith", "bob@example.com", type.Id);
        var response = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var rel = await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipDto>();
        rel!.EmployeeName.Should().Be("Bob Smith");
        rel.EmployeeEmail.Should().Be("bob@example.com");
        rel.RelationshipTypeName.Should().Be("Sales Rep");
        rel.ContactId.Should().Be(contact.Id);
    }

    [Fact]
    public async Task CreateRelationship_WithNotes_PersistsNotes()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var type = await CreateTypeAsync(client, "Customer Support");
        var contact = await CreateTestContactAsync(client);

        var request = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Barb Jones", "barb@example.com", type.Id,
            Notes: "Primary phone support contact");
        var response = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var rel = await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipDto>();
        rel!.Notes.Should().Be("Primary phone support contact");
    }

    [Fact]
    public async Task CreateRelationship_WithoutNotes_NotesIsNull()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var type = await CreateTypeAsync(client, "Account Manager");
        var contact = await CreateTestContactAsync(client);

        var request = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Ralph Davis", null, type.Id);
        var response = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var rel = await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipDto>();
        rel!.Notes.Should().BeNull();
        rel.EmployeeEmail.Should().BeNull();
    }

    [Fact]
    public async Task CreateRelationship_MultipleEmployees_SameContact()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var salesType = await CreateTypeAsync(client, "Sales");
        var supportType = await CreateTypeAsync(client, "Support");
        var contact = await CreateTestContactAsync(client);

        // Assign two different employees with different roles
        var req1 = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Bob Sales", "bob@acme.com", salesType.Id, "Handles all sales");
        var req2 = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Alice Support", "alice@acme.com", supportType.Id, "Phone support");

        var resp1 = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", req1);
        var resp2 = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", req2);

        resp1.StatusCode.Should().Be(HttpStatusCode.Created);
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify both are listed
        var getResponse = await client.GetAsync($"api/contacts/{contact.Id}/relationships");
        var relationships = await getResponse.Content.ReadFromJsonAsync<List<ContactEmployeeRelationshipDto>>();
        relationships.Should().HaveCount(2);
        relationships.Should().Contain(r => r.EmployeeName == "Bob Sales" && r.RelationshipTypeName == "Sales");
        relationships.Should().Contain(r => r.EmployeeName == "Alice Support" && r.RelationshipTypeName == "Support");
    }

    [Fact]
    public async Task DeleteRelationship_ReturnsNoContent()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var type = await CreateTypeAsync(client, "Temporary");
        var contact = await CreateTestContactAsync(client);

        var request = new CreateContactEmployeeRelationshipRequest(
            Guid.NewGuid(), "Temp Employee", null, type.Id);
        var createResponse = await client.PostAsJsonAsync($"api/contacts/{contact.Id}/relationships", request);
        var rel = await createResponse.Content.ReadFromJsonAsync<ContactEmployeeRelationshipDto>();

        var response = await client.DeleteAsync($"api/contacts/{contact.Id}/relationships/{rel!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await client.GetAsync($"api/contacts/{contact.Id}/relationships");
        var remaining = await getResponse.Content.ReadFromJsonAsync<List<ContactEmployeeRelationshipDto>>();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteRelationship_ReturnsNotFound_WhenDoesNotExist()
    {
        var (client, _) = await AuthenticateAsync();
        var contact = await CreateTestContactAsync(client);

        var response = await client.DeleteAsync($"api/contacts/{contact.Id}/relationships/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRelationships_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"api/contacts/{Guid.NewGuid()}/relationships");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRelationshipTypes_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/contact-relationship-types");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static async Task<ContactEmployeeRelationshipTypeDto> CreateTypeAsync(
        HttpClient client, string name)
    {
        var request = new CreateContactEmployeeRelationshipTypeRequest(name);
        var response = await client.PostAsJsonAsync("api/contact-relationship-types", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipTypeDto>())!;
    }

    private static async Task<ContactDto> CreateTestContactAsync(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var request = new CreateContactRequest(
            $"Test-{tag}", "Contact",
            $"contact-{Guid.NewGuid()}@example.com", null, null, null);
        var response = await client.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }
}
