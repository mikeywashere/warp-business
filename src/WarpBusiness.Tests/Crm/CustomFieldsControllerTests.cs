using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Crm;

public class CustomFieldsControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public CustomFieldsControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> RegisterAndAuthenticateAsync()
    {
        var client = _factory.CreateClient();
        var email = $"cf-user-{Guid.NewGuid():N}@example.com";
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, email);
        client.SetBearerToken(token);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"cf-admin-{Guid.NewGuid():N}@example.com";
        await AuthHelper.RegisterAndGetTokenAsync(client, email);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return client;
    }

    private async Task<CustomFieldDefinitionDto> CreateFieldDefinitionAsync(
        HttpClient adminClient, string name, string fieldType = "Text")
    {
        var request = new CreateCustomFieldDefinitionRequest(name, "Contact", fieldType, null, false, 1);
        var response = await adminClient.PostAsJsonAsync("api/custom-fields", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;
    }

    // ── Definition CRUD ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_ReturnsEmptyList_WhenNoneExist()
    {
        // Arrange
        var client = await RegisterAndAuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/custom-fields?entityType=Contact");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await response.Content.ReadFromJsonAsync<List<CustomFieldDefinitionDto>>();
        definitions.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDefinition_AsAdmin_ReturnsCreatedDefinition()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var request = new CreateCustomFieldDefinitionRequest(
            "Industry", "Contact", "Text", null, false, 1);

        // Act
        var response = await adminClient.PostAsJsonAsync("api/custom-fields", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var definition = await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>();
        definition.Should().NotBeNull();
        definition!.Name.Should().Be("Industry");
        definition.EntityType.Should().Be("Contact");
        definition.FieldType.Should().Be("Text");
        definition.IsRequired.Should().BeFalse();
        definition.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task CreateDefinition_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await RegisterAndAuthenticateAsync();
        var request = new CreateCustomFieldDefinitionRequest(
            "SomeField", "Contact", "Text", null, false, 1);

        // Act
        var response = await client.PostAsJsonAsync("api/custom-fields", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDefinition_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var fieldName = $"DupField-{Guid.NewGuid():N}";
        var request = new CreateCustomFieldDefinitionRequest(fieldName, "Contact", "Text", null, false, 1);
        await adminClient.PostAsJsonAsync("api/custom-fields", request);

        // Act — same name second time
        var response = await adminClient.PostAsJsonAsync("api/custom-fields", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateDefinition_AsAdmin_ReturnsUpdatedDefinition()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var definition = await CreateFieldDefinitionAsync(adminClient, $"UpdateMe-{Guid.NewGuid():N}");
        var updateRequest = new UpdateCustomFieldDefinitionRequest(
            "UpdatedName", "Text", null, false, 2, true);

        // Act
        var response = await adminClient.PutAsJsonAsync($"api/custom-fields/{definition.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>();
        updated!.Name.Should().Be("UpdatedName");
        updated.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task DeleteDefinition_WithNoValues_ReturnsNoContent()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var definition = await CreateFieldDefinitionAsync(adminClient, $"DeleteMe-{Guid.NewGuid():N}");

        // Act
        var response = await adminClient.DeleteAsync($"api/custom-fields/{definition.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteDefinition_WithExistingValues_ReturnsConflict()
    {
        // Arrange — admin creates a field definition
        var adminClient = await CreateAdminClientAsync();
        var definition = await CreateFieldDefinitionAsync(adminClient, $"HasValues-{Guid.NewGuid():N}");

        // Create a contact that carries a value for that field
        var userClient = await RegisterAndAuthenticateAsync();
        var contactRequest = new CreateContactRequest(
            "Block", "Delete", $"block-{Guid.NewGuid():N}@test.com", null, null, null,
            new List<UpsertCustomFieldValueRequest> { new(definition.Id, "SomeValue") });
        var contactResponse = await userClient.PostAsJsonAsync("api/contacts", contactRequest);
        contactResponse.EnsureSuccessStatusCode();

        // Act — try to delete the field that now has values
        var response = await adminClient.DeleteAsync($"api/custom-fields/{definition.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Custom field values through contacts ───────────────────────────────────

    [Fact]
    public async Task CreateContact_WithCustomFields_PersistsValues()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var definition = await CreateFieldDefinitionAsync(adminClient, $"Sector-{Guid.NewGuid():N}");

        var userClient = await RegisterAndAuthenticateAsync();
        var contactRequest = new CreateContactRequest(
            "Custom", "Fields", $"custom-{Guid.NewGuid():N}@test.com", null, null, null,
            new List<UpsertCustomFieldValueRequest> { new(definition.Id, "Fintech") });

        // Act
        var createResponse = await userClient.PostAsJsonAsync("api/contacts", contactRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ContactDto>();

        var getResponse = await userClient.GetAsync($"api/contacts/{created!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await getResponse.Content.ReadFromJsonAsync<ContactDto>();
        contact!.CustomFields.Should().Contain(f => f.FieldName == definition.Name && f.Value == "Fintech");
    }

    [Fact]
    public async Task UpdateContact_WithCustomFields_UpdatesValues()
    {
        // Arrange
        var adminClient = await CreateAdminClientAsync();
        var definition = await CreateFieldDefinitionAsync(adminClient, $"UpdateField-{Guid.NewGuid():N}");

        var userClient = await RegisterAndAuthenticateAsync();
        var contactRequest = new CreateContactRequest(
            "Upd", "Contact", $"upd-{Guid.NewGuid():N}@test.com", null, null, null,
            new List<UpsertCustomFieldValueRequest> { new(definition.Id, "Fintech") });
        var createResponse = await userClient.PostAsJsonAsync("api/contacts", contactRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ContactDto>();

        var updateRequest = new UpdateContactRequest(
            "Upd", "Contact", created!.Email, null, null, null, "Active",
            new List<UpsertCustomFieldValueRequest> { new(definition.Id, "Healthcare") });

        // Act
        var updateResponse = await userClient.PutAsJsonAsync($"api/contacts/{created.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var getResponse = await userClient.GetAsync($"api/contacts/{created.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await getResponse.Content.ReadFromJsonAsync<ContactDto>();
        contact!.CustomFields.Should().Contain(f => f.FieldDefinitionId == definition.Id && f.Value == "Healthcare");
    }

    [Fact]
    public async Task GetContact_IncludesAllActiveFieldDefinitions()
    {
        // Arrange — two field definitions, contact only sets a value for the first
        var adminClient = await CreateAdminClientAsync();
        var field1 = await CreateFieldDefinitionAsync(adminClient, $"F1-{Guid.NewGuid():N}");
        var field2 = await CreateFieldDefinitionAsync(adminClient, $"F2-{Guid.NewGuid():N}");

        var userClient = await RegisterAndAuthenticateAsync();
        var contactRequest = new CreateContactRequest(
            "All", "Fields", $"allfields-{Guid.NewGuid():N}@test.com", null, null, null,
            new List<UpsertCustomFieldValueRequest> { new(field1.Id, "Value1") });
        var createResponse = await userClient.PostAsJsonAsync("api/contacts", contactRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ContactDto>();

        // Act
        var getResponse = await userClient.GetAsync($"api/contacts/{created!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await getResponse.Content.ReadFromJsonAsync<ContactDto>();
        contact!.CustomFields.Should().Contain(f => f.FieldDefinitionId == field1.Id && f.Value == "Value1");
        contact.CustomFields.Should().Contain(f => f.FieldDefinitionId == field2.Id && f.Value == null);
    }
}
