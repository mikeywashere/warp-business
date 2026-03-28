using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarpBusiness.Api.Data;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Domain;
using WarpBusiness.Plugin.EmployeeManagement.Controllers;
using WarpBusiness.Plugin.EmployeeManagement.Data;
using WarpBusiness.Plugin.EmployeeManagement.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Tests.Tenancy;

/// <summary>
/// Verifies that EF Core global query filters enforce tenant data isolation.
///
/// Architecture under test:
///   - CrmDbContext.HasQueryFilter uses ITenantContext.TenantId
///   - JwtTenantContext reads tenant_id from the bearer token
///   - Services must set entity.TenantId from ITenantContext on create/update
///
/// TODO (Hicks): CompanyService, ContactService, and EmployeeService must set
/// entity.TenantId = _tenantContext.TenantId when creating records. Until that
/// is done, the tests that create data via the API will fail because entities
/// are stored with TenantId = Guid.Empty.
/// </summary>
[Trait("Category", "Tenancy")]
public class CrossTenantIsolationTests : IClassFixture<MultiTenantTestFixture>
{
    private readonly MultiTenantTestFixture _fix;

    public CrossTenantIsolationTests(MultiTenantTestFixture fixture)
    {
        _fix = fixture;
    }

    // ── Company isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCompanies_ReturnsOnlyCurrentTenantCompanies()
    {
        // Arrange — seed one company per tenant directly with the correct TenantId
        var companyAId = await SeedCompanyAsync(_fix.TenantAId, $"AlphaCo-{Guid.NewGuid()}");
        var companyBId = await SeedCompanyAsync(_fix.TenantBId, $"BetaCo-{Guid.NewGuid()}");

        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);

        // Act
        var responseA = await clientA.GetAsync("api/companies");
        var responseB = await clientB.GetAsync("api/companies");

        // Assert
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultA = await responseA.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>();
        var resultB = await responseB.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>();

        resultA!.Items.Select(c => c.Id).Should().Contain(companyAId,
            "TenantA should see its own company");
        resultA.Items.Select(c => c.Id).Should().NotContain(companyBId,
            "TenantA must not see TenantB's company");

        resultB!.Items.Select(c => c.Id).Should().Contain(companyBId,
            "TenantB should see its own company");
        resultB.Items.Select(c => c.Id).Should().NotContain(companyAId,
            "TenantB must not see TenantA's company");
    }

    [Fact]
    public async Task GetCompanyById_TenantA_CannotAccessTenantBCompany_Returns404()
    {
        // Arrange — seed a TenantB company directly in the CRM DbContext
        var tenantBCompanyId = await SeedCompanyAsync(_fix.TenantBId, $"SecretBetaCo-{Guid.NewGuid()}");

        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);

        // Act — TenantA user tries to read TenantB's company by ID
        var response = await clientA.GetAsync($"api/companies/{tenantBCompanyId}");

        // Assert — global query filter hides the record
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "HasQueryFilter on TenantId should make cross-tenant records invisible");
    }

    [Fact]
    public async Task UpdateCompany_TenantB_CannotModifyTenantACompany_Returns404()
    {
        // Arrange — seed a TenantA company
        var tenantACompanyId = await SeedCompanyAsync(_fix.TenantAId, $"AlphaTarget-{Guid.NewGuid()}");

        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);
        var updateRequest = new UpdateCompanyRequest("HijackedName", null, null, null, null, null);

        // Act — TenantB user tries to update TenantA's company
        var response = await clientB.PutAsJsonAsync($"api/companies/{tenantACompanyId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "update should find no record when the query filter excludes it");
    }

    [Fact]
    public async Task DeleteCompany_TenantB_CannotDeleteTenantACompany_Returns404()
    {
        // Arrange — create a TenantA company; give TenantB user Admin role so role isn't the barrier
        var tenantACompanyId = await SeedCompanyAsync(_fix.TenantAId, $"AlphaDeleteTarget-{Guid.NewGuid()}");
        var adminClientB = await _fix.CreateAdminClientAsync(
            _fix.UserBEmail, _fix.TenantBId, _fix.TenantBSlug);

        // Act — TenantB admin tries to delete TenantA's company
        var response = await adminClientB.DeleteAsync($"api/companies/{tenantACompanyId}");

        // Assert — query filter returns 404 before the delete logic executes
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "global query filter should hide the record regardless of Admin role");
    }

    [Fact]
    public async Task CreateCompany_TenantIdSetFromJwt_NotFromRequestBody()
    {
        // Arrange
        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);
        var companyName = $"JwtTenantTest-{Guid.NewGuid()}";

        // TODO (Hicks): CompanyService.CreateCompanyAsync must set
        // company.TenantId = _tenantContext.TenantId before saving.
        // Until then, TenantId will be Guid.Empty and the query filter
        // will hide the record from both tenants.

        // Act — UserA creates a company; TenantId must come from JWT, not body
        var createResponse = await clientA.PostAsJsonAsync("api/companies",
            new CreateCompanyRequest(companyName, null, null, null, null, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CompanyDto>();

        // Assert — TenantB cannot see it (proves TenantId was set from JWT)
        var crossTenantGet = await clientB.GetAsync($"api/companies/{created!.Id}");
        crossTenantGet.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "company must be invisible to other tenants");

        // And TenantA can see it
        var ownGet = await clientA.GetAsync($"api/companies/{created.Id}");
        ownGet.StatusCode.Should().Be(HttpStatusCode.OK,
            "company must be visible within its own tenant");
    }

    [Fact]
    public async Task CreateCompany_SameName_DifferentTenants_BothSucceed()
    {
        // Arrange
        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);
        var sharedName = $"SharedName-{Guid.NewGuid()}";

        // TODO (Hicks): requires CompanyService to set TenantId from ITenantContext.
        // The uniqueness check in the controller is (Name + TenantId), so this
        // should succeed for both tenants once TenantId is correctly assigned.

        // Act
        var responseA = await clientA.PostAsJsonAsync("api/companies",
            new CreateCompanyRequest(sharedName, null, null, null, null, null));
        var responseB = await clientB.PostAsJsonAsync("api/companies",
            new CreateCompanyRequest(sharedName, null, null, null, null, null));

        // Assert — both succeed; uniqueness is scoped per tenant, not global
        responseA.StatusCode.Should().Be(HttpStatusCode.Created,
            "TenantA should be allowed to use any name");
        responseB.StatusCode.Should().Be(HttpStatusCode.Created,
            "TenantB should independently be allowed the same company name");
    }

    [Fact(Skip = "Company uniqueness check needs TenantId scoping — see TODO in test")]
    public async Task CreateCompany_SameName_SameTenant_Returns409()
    {
        // Arrange
        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var duplicateName = $"DuplicateTest-{Guid.NewGuid()}";

        // TODO (Hicks): requires CompanyService to set TenantId from ITenantContext.
        // The controller's existing AnyAsync uniqueness guard uses Name + OwnerId,
        // not Name + TenantId. It must be updated to scope uniqueness per tenant.

        var firstCreate = await clientA.PostAsJsonAsync("api/companies",
            new CreateCompanyRequest(duplicateName, null, null, null, null, null));
        firstCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — same tenant, same name again
        var secondCreate = await clientA.PostAsJsonAsync("api/companies",
            new CreateCompanyRequest(duplicateName, null, null, null, null, null));

        // Assert
        secondCreate.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "company names must be unique within a tenant");
    }

    // ── Contact isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetContacts_ReturnsOnlyCurrentTenantContacts()
    {
        // Arrange — seed one contact per tenant directly in CRM DbContext
        var contactAId = await SeedContactAsync(_fix.TenantAId, "Alice", "Atkins", $"a-{Guid.NewGuid()}@example.com");
        var contactBId = await SeedContactAsync(_fix.TenantBId, "Bob", "Brown", $"b-{Guid.NewGuid()}@example.com");

        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);

        // Act
        var responseA = await clientA.GetAsync("api/contacts");
        var responseB = await clientB.GetAsync("api/contacts");

        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultA = await responseA.Content.ReadFromJsonAsync<PagedResult<ContactDto>>();
        var resultB = await responseB.Content.ReadFromJsonAsync<PagedResult<ContactDto>>();

        resultA!.Items.Select(c => c.Id).Should().Contain(contactAId,
            "TenantA should see its own contact");
        resultA.Items.Select(c => c.Id).Should().NotContain(contactBId,
            "TenantA must not see TenantB's contact");

        resultB!.Items.Select(c => c.Id).Should().Contain(contactBId,
            "TenantB should see its own contact");
        resultB.Items.Select(c => c.Id).Should().NotContain(contactAId,
            "TenantB must not see TenantA's contact");
    }

    // ── Employee isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployees_ReturnsOnlyCurrentTenantEmployees()
    {
        // Arrange — seed one employee per tenant directly in Employee DbContext
        var empAId = await SeedEmployeeAsync(_fix.TenantAId, "Alice", "Atkins", $"emp-a-{Guid.NewGuid()}@example.com");
        var empBId = await SeedEmployeeAsync(_fix.TenantBId, "Bob", "Brown", $"emp-b-{Guid.NewGuid()}@example.com");

        var clientA = _fix.CreateClientWithToken(_fix.UserAToken);
        var clientB = _fix.CreateClientWithToken(_fix.UserBToken);

        // Act
        var responseA = await clientA.GetAsync("api/employees");
        var responseB = await clientB.GetAsync("api/employees");

        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        // Employees controller returns anonymous objects; inspect via JsonElement
        var resultA = await responseA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var resultB = await responseB.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var idsA = resultA.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
        var idsB = resultB.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid()).ToHashSet();

        idsA.Should().Contain(empAId, "TenantA sees its own employee");
        idsA.Should().NotContain(empBId, "TenantA must not see TenantB's employee");

        idsB.Should().Contain(empBId, "TenantB sees its own employee");
        idsB.Should().NotContain(empAId, "TenantB must not see TenantA's employee");
    }

    // ── DB seed helpers (bypass API to control TenantId directly) ─────────────

    private async Task<Guid> SeedCompanyAsync(Guid tenantId, string name)
    {
        using var scope = _fix.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        // IgnoreQueryFilters is required when seeding — the context's filter would
        // block writes from the wrong tenant scope
        var company = new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            CreatedBy = "test-seeder",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Add directly via EF, bypassing the query filter (seed path)
        await db.Companies.AddAsync(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<Guid> SeedContactAsync(Guid tenantId, string first, string last, string email)
    {
        using var scope = _fix.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = first,
            LastName = last,
            Email = email,
            Status = ContactStatus.Active,
            CreatedBy = "test-seeder",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await db.Contacts.AddAsync(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    private async Task<Guid> SeedEmployeeAsync(Guid tenantId, string first, string last, string email)
    {
        using var scope = _fix.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = first,
            LastName = last,
            Email = email,
            HireDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await db.Employees.AddAsync(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }
}
