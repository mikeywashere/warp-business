using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Api.Controllers;

namespace WarpBusiness.Tests.Tenancy;

/// <summary>
/// Tests for tenant lifecycle: signup, slug uniqueness, and role-based management.
///
/// These tests use the POST /api/tenants/signup endpoint rather than the fixture's
/// pre-seeded tenants, so each test is fully self-contained.
/// </summary>
[Trait("Category", "Tenancy")]
public class TenantManagementTests : IClassFixture<MultiTenantTestFixture>
{
    private readonly MultiTenantTestFixture _fix;

    public TenantManagementTests(MultiTenantTestFixture fixture)
    {
        _fix = fixture;
    }

    [Fact]
    public async Task Signup_CreatesNewTenant_CreatorBecomesAdmin()
    {
        // Arrange — use a regular authenticated user (UserA's no-tenant token is fine for signup)
        var client = _fix.CreateClientWithToken(_fix.NoTenantToken);
        var slug = "new-co-" + Guid.NewGuid().ToString("N")[..8];

        // Act
        var response = await client.PostAsJsonAsync(
            "api/tenants/signup",
            new TenantSignupRequest("New Company Inc", slug));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "signup should succeed for a new unique slug");

        var result = await response.Content.ReadFromJsonAsync<TenantSignupResponse>();
        result.Should().NotBeNull();
        result!.TenantId.Should().NotBeEmpty();
        result.Slug.Should().Be(slug);
        result.Role.Should().Be("TenantAdmin",
            "the creator should automatically become TenantAdmin");
        result.AccessToken.Should().NotBeNullOrWhiteSpace(
            "a tenant-scoped JWT should be returned immediately");
    }

    [Fact]
    public async Task Signup_DuplicateSlug_Returns409()
    {
        // Arrange — first signup creates the slug
        var client = _fix.CreateClientWithToken(_fix.NoTenantToken);
        var slug = "dup-slug-" + Guid.NewGuid().ToString("N")[..8];

        var first = await client.PostAsJsonAsync(
            "api/tenants/signup",
            new TenantSignupRequest("Original Co", slug));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — second signup with the same slug (fresh client to avoid stale state)
        var client2 = _fix.CreateClientWithToken(_fix.SharedUserTokenB);
        var second = await client2.PostAsJsonAsync(
            "api/tenants/signup",
            new TenantSignupRequest("Duplicate Co", slug));

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "slugs must be globally unique across all tenants");
    }

    [Fact]
    public async Task Signup_InvalidSlugFormat_Returns400()
    {
        // Arrange — slug with uppercase letters (invalid per slug rules)
        var client = _fix.CreateClientWithToken(_fix.NoTenantToken);

        var response = await client.PostAsJsonAsync(
            "api/tenants/signup",
            new TenantSignupRequest("Bad Slug Co", "UpperCase-Slug"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "slugs must be lowercase alphanumeric with hyphens only");
    }

    [Fact]
    public async Task UpdateTenant_TenantAdmin_Succeeds()
    {
        // Arrange — UserA is TenantAdmin of TenantA (set up in fixture)
        var adminClientA = await _fix.CreateAdminClientAsync(
            _fix.UserAEmail, _fix.TenantAId, _fix.TenantASlug);

        // Act — update TenantA's display name
        var response = await adminClientA.PutAsJsonAsync(
            $"api/tenants/{_fix.TenantAId}",
            new UpdateTenantRequest("Tenant Alpha Updated", "Alpha Display"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "TenantAdmin should be allowed to update their own tenant");
    }

    [Fact]
    public async Task UpdateTenant_Member_Returns403()
    {
        // Arrange — SharedUser is a Member (not TenantAdmin) in TenantA
        var memberClient = _fix.CreateClientWithToken(_fix.SharedUserTokenA);

        // Act
        var response = await memberClient.PutAsJsonAsync(
            $"api/tenants/{_fix.TenantAId}",
            new UpdateTenantRequest("Unauthorised Name Change", null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "non-admin members must not be allowed to update tenant settings");
    }

    [Fact]
    public async Task UpdateTenant_AdminOfOtherTenant_Returns403()
    {
        // Arrange — UserB is TenantAdmin of TenantB, not TenantA
        // Give UserB the Admin app-role so the only blocker is tenant membership
        var adminClientB = await _fix.CreateAdminClientAsync(
            _fix.UserBEmail, _fix.TenantBId, _fix.TenantBSlug);

        // Act — attempt to update TenantA while authenticated to TenantB
        var response = await adminClientB.PutAsJsonAsync(
            $"api/tenants/{_fix.TenantAId}",
            new UpdateTenantRequest("Cross-tenant Hijack", null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "being TenantAdmin of TenantB does not grant rights over TenantA");
    }

    [Fact]
    public async Task GetTenantDetails_TenantAdmin_ReturnsMembers()
    {
        // Arrange — UserA is TenantAdmin of TenantA
        var adminClientA = await _fix.CreateAdminClientAsync(
            _fix.UserAEmail, _fix.TenantAId, _fix.TenantASlug);

        // Act
        var response = await adminClientA.GetAsync($"api/tenants/{_fix.TenantAId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<TenantDetailDto>();
        detail.Should().NotBeNull();
        detail!.TenantId.Should().Be(_fix.TenantAId);
        detail.Members.Should().NotBeEmpty("TenantA has at least UserA and SharedUser as members");
    }

    [Fact]
    public async Task GetTenantDetails_Member_Returns403()
    {
        // Arrange — SharedUser is a Member, not TenantAdmin, in TenantA
        var memberClient = _fix.CreateClientWithToken(_fix.SharedUserTokenA);

        // Act
        var response = await memberClient.GetAsync($"api/tenants/{_fix.TenantAId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "only TenantAdmin should see the full tenant detail with member list");
    }

    [Fact]
    public async Task AddMember_TenantAdmin_Succeeds()
    {
        // Arrange — create a brand-new user to be added
        var newUserEmail = $"new-member-{Guid.NewGuid():N}@example.com";
        var setupClient = _fix.Factory.CreateClient();
        var registerResponse = await setupClient.PostAsJsonAsync("api/auth/register",
            new WarpBusiness.Shared.Auth.RegisterRequest(newUserEmail, "Test1234!", "New", "Member"));
        registerResponse.EnsureSuccessStatusCode();

        var adminClientA = await _fix.CreateAdminClientAsync(
            _fix.UserAEmail, _fix.TenantAId, _fix.TenantASlug);

        // Act
        var response = await adminClientA.PostAsJsonAsync(
            $"api/tenants/{_fix.TenantAId}/members",
            new AddMemberRequest(newUserEmail, "Member"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "TenantAdmin should be able to add new members");
    }

    [Fact]
    public async Task AddMember_DuplicateMember_Returns409()
    {
        // Arrange — SharedUser is already a member of TenantA
        var adminClientA = await _fix.CreateAdminClientAsync(
            _fix.UserAEmail, _fix.TenantAId, _fix.TenantASlug);

        // Act — try to add SharedUser again
        var response = await adminClientA.PostAsJsonAsync(
            $"api/tenants/{_fix.TenantAId}/members",
            new AddMemberRequest(_fix.SharedUserEmail, "Member"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "adding a user who is already a member must return 409");
    }
}
