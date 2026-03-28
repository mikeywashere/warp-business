using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Tests.Tenancy;

/// <summary>
/// Tests for JWT tenant claim validation, tenant selection, and multi-tenant user flows.
///
/// Architecture under test:
///   - JwtTenantContext reads tenant_id claim from bearer token
///   - TenantsController.GetMine returns the user's tenant memberships
///   - TenantsController.Signup issues a tenant-scoped JWT
///
/// TODO (Hicks): A tenant membership validation middleware is needed to enforce:
///   - 403 when tenant_id in JWT doesn't match any existing tenant (InvalidTenantId)
///   - 403 when user holds a valid tenant_id JWT but is not a member of that tenant
/// Without the middleware, the query filter simply returns empty results (not 403).
/// Tests annotated [TODO-MIDDLEWARE] assert the correct 403 behaviour; they will
/// fail until the middleware is in place.
/// </summary>
[Trait("Category", "Tenancy")]
public class TenantAuthTests : IClassFixture<MultiTenantTestFixture>
{
    private readonly MultiTenantTestFixture _fix;

    public TenantAuthTests(MultiTenantTestFixture fixture)
    {
        _fix = fixture;
    }

    [Fact(Skip = "Requires tenant membership validation middleware — see TODO-MIDDLEWARE")]
    public async Task DataEndpoints_NoTenantClaim_Returns403()
    {
        // TODO-MIDDLEWARE: Requires a middleware that rejects requests with no
        // tenant_id claim on data endpoints. Currently returns 200 (empty list)
        // because the query filter matches Guid.Empty == Guid.Empty for unscoped data.

        var client = _fix.CreateClientWithToken(_fix.NoTenantToken);

        var response = await client.GetAsync("api/companies");

        // Expected once middleware is in place:
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "authenticated users without a tenant_id claim must be blocked from data endpoints");
    }

    [Fact(Skip = "Requires tenant membership validation middleware — see TODO-MIDDLEWARE")]
    public async Task DataEndpoints_InvalidTenantId_Returns403()
    {
        // TODO-MIDDLEWARE: Requires middleware that validates tenant_id in JWT
        // corresponds to a real, active tenant in the database.
        // Currently returns 200 with empty results (no records match a random GUID).

        var client = _fix.CreateClientWithToken(_fix.InvalidTenantToken);

        var response = await client.GetAsync("api/companies");

        // Expected once middleware is in place:
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a tenant_id that doesn't exist in the database must be rejected");
    }

    [Fact(Skip = "Requires tenant membership validation middleware — see TODO-MIDDLEWARE")]
    public async Task DataEndpoints_TenantIdNotMember_Returns403()
    {
        // TODO-MIDDLEWARE: Requires middleware that checks UserTenants membership.
        // UserA has a JWT scoped to TenantB but is not a member of TenantB.
        // Currently returns 200 with empty results (TenantB data is filtered out).

        var client = _fix.CreateClientWithToken(_fix.WrongMemberToken); // UserA → TenantB

        var response = await client.GetAsync("api/companies");

        // Expected once middleware is in place:
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a user must not be able to claim a tenant they are not a member of");
    }

    [Fact]
    public async Task GetMyTenants_ReturnsAllUserTenants()
    {
        // Arrange — SharedUser is a member of both TenantA and TenantB
        var client = _fix.CreateClientWithToken(_fix.SharedUserTokenA);

        // Act
        var response = await client.GetAsync("api/tenants/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenants = await response.Content.ReadFromJsonAsync<IEnumerable<TenantSummaryDto>>();
        tenants.Should().NotBeNull();

        var tenantList = tenants!.ToList();
        tenantList.Should().HaveCount(2, "SharedUser belongs to both TenantA and TenantB");
        tenantList.Select(t => t.TenantId).Should().Contain(_fix.TenantAId);
        tenantList.Select(t => t.TenantId).Should().Contain(_fix.TenantBId);
    }

    [Fact]
    public async Task GetMyTenants_UserA_ReturnsOnlyTenantA()
    {
        // Arrange — UserA is a member of TenantA only
        var client = _fix.CreateClientWithToken(_fix.UserAToken);

        // Act
        var response = await client.GetAsync("api/tenants/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenants = await response.Content.ReadFromJsonAsync<IEnumerable<TenantSummaryDto>>();
        var tenantList = tenants!.ToList();

        tenantList.Should().HaveCount(1, "UserA belongs to TenantA only");
        tenantList.Single().TenantId.Should().Be(_fix.TenantAId);
    }

    [Fact]
    public async Task SelectTenant_SharedUser_CanAccessEitherTenant()
    {
        // Arrange — SharedUser uses TenantA-scoped token, then TenantB-scoped token
        var clientA = _fix.CreateClientWithToken(_fix.SharedUserTokenA);
        var clientB = _fix.CreateClientWithToken(_fix.SharedUserTokenB);

        // Act — request each tenant's company list
        var responseA = await clientA.GetAsync("api/companies");
        var responseB = await clientB.GetAsync("api/companies");

        // Assert — both succeed; query filter scopes results to the token's tenant
        responseA.StatusCode.Should().Be(HttpStatusCode.OK,
            "SharedUser with TenantA token should access TenantA data");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK,
            "SharedUser with TenantB token should access TenantB data");
    }

    [Fact]
    public async Task GetMyTenants_RequiresAuthentication()
    {
        // Arrange — unauthenticated client
        var anonClient = _fix.Factory.CreateClient();

        // Act
        var response = await anonClient.GetAsync("api/tenants/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
