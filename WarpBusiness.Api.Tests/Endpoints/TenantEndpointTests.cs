using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Endpoints;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace WarpBusiness.Api.Tests.Endpoints;

[Collection("Database")]
public class TenantEndpointTests
{
    private readonly PostgreSqlFixture _fixture;

    public TenantEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<WarpBusinessDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();
        // Clean slate
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();
        return db;
    }

    private async Task<(ApplicationUser Admin, ApplicationUser RegularUser, Tenant Tenant)> SeedTestData(WarpBusinessDbContext db)
    {
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = "admin-sub-id",
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@warpindustries.com",
            Role = UserRole.SystemAdministrator
        };

        var regularUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = "user-sub-id",
            FirstName = "Regular",
            LastName = "User",
            Email = "user@warpindustries.com",
            Role = UserRole.User
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Slug = "test-tenant",
            IsActive = true
        };

        db.Users.AddRange(admin, regularUser);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Add regular user as member
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = regularUser.Id,
            TenantId = tenant.Id,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return (admin, regularUser, tenant);
    }

    [Fact]
    public async Task GetAllTenants_Admin_SeesAllTenants()
    {
        await using var db = await CreateCleanContext();
        var (admin, _, tenant) = await SeedTestData(db);
        var secondTenant = new Tenant { Id = Guid.NewGuid(), Name = "Second", Slug = "second", IsActive = true };
        db.Tenants.Add(secondTenant);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateAdminPrincipal();

        // Call the endpoint method directly via reflection or by invoking the static method
        // We use the TenantEndpoints static methods through the mapped route handlers
        var result = await CallGetAllTenants(db, principal);

        result.Should().BeOfType<Ok<List<TenantResponse>>>();
        var okResult = (Ok<List<TenantResponse>>)result;
        okResult.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetAllTenants_RegularUser_SeesOnlyMemberTenants()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        // Add another tenant the user is NOT a member of
        var otherTenant = new Tenant { Id = Guid.NewGuid(), Name = "Other", Slug = "other", IsActive = true };
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);

        var result = await CallGetAllTenants(db, principal);

        result.Should().BeOfType<Ok<List<TenantResponse>>>();
        var okResult = (Ok<List<TenantResponse>>)result;
        okResult.Value!.Count.Should().Be(1);
        okResult.Value.First().Slug.Should().Be("test-tenant");
    }

    [Fact]
    public async Task GetTenantById_Member_ReturnsTenant()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);

        var result = await CallGetTenantById(db, tenant.Id, principal);

        result.Should().BeOfType<Ok<TenantResponse>>();
    }

    [Fact]
    public async Task GetTenantById_NonMember_ReturnsForbid()
    {
        await using var db = await CreateCleanContext();
        await SeedTestData(db);

        var nonMemberTenant = new Tenant { Id = Guid.NewGuid(), Name = "Secret", Slug = "secret", IsActive = true };
        db.Tenants.Add(nonMemberTenant);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal("user@warpindustries.com", "user-sub-id");

        var result = await CallGetTenantById(db, nonMemberTenant.Id, principal);

        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task CreateTenant_Success()
    {
        await using var db = await CreateCleanContext();
        var request = new CreateTenantRequest("New Corp", "new-corp");

        var result = await CallCreateTenant(db, request);

        result.Should().BeOfType<Created<TenantResponse>>();
        var created = (Created<TenantResponse>)result;
        created.Value!.Slug.Should().Be("new-corp");
    }

    [Fact]
    public async Task CreateTenant_DuplicateSlug_Returns409()
    {
        await using var db = await CreateCleanContext();
        await SeedTestData(db);

        var request = new CreateTenantRequest("Duplicate", "test-tenant");

        var result = await CallCreateTenant(db, request);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateTenant_Success()
    {
        await using var db = await CreateCleanContext();
        var (_, _, tenant) = await SeedTestData(db);

        var request = new UpdateTenantRequest("Updated Name", "test-tenant", true);

        var result = await CallUpdateTenant(db, tenant.Id, request);

        result.Should().BeOfType<Ok<TenantResponse>>();
        var okResult = (Ok<TenantResponse>)result;
        okResult.Value!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateTenant_DuplicateSlug_Returns409()
    {
        await using var db = await CreateCleanContext();
        await SeedTestData(db);
        var secondTenant = new Tenant { Id = Guid.NewGuid(), Name = "Second", Slug = "second", IsActive = true };
        db.Tenants.Add(secondTenant);
        await db.SaveChangesAsync();

        var request = new UpdateTenantRequest("Second", "test-tenant", true);

        var result = await CallUpdateTenant(db, secondTenant.Id, request);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task DeleteTenant_Success_CascadesMemberships()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var tenantId = tenant.Id;
        var result = await CallDeleteTenant(db, tenantId);

        result.Should().BeOfType<NoContent>();

        var memberships = await db.UserTenantMemberships
            .Where(m => m.TenantId == tenantId)
            .ToListAsync();

        memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantMembers_Admin_ReturnsMemberList()
    {
        await using var db = await CreateCleanContext();
        var (admin, regularUser, tenant) = await SeedTestData(db);

        var principal = TestHelpers.CreateAdminPrincipal();

        var result = await CallGetTenantMembers(db, tenant.Id, principal);

        result.Should().BeOfType<Ok<List<TenantMemberResponse>>>();
        var okResult = (Ok<List<TenantMemberResponse>>)result;
        okResult.Value!.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetTenantMembers_NonMember_ReturnsForbid()
    {
        await using var db = await CreateCleanContext();
        await SeedTestData(db);

        var nonMemberTenant = new Tenant { Id = Guid.NewGuid(), Name = "Secret", Slug = "secret", IsActive = true };
        db.Tenants.Add(nonMemberTenant);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal("user@warpindustries.com", "user-sub-id");

        var result = await CallGetTenantMembers(db, nonMemberTenant.Id, principal);

        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task AddTenantMember_Success()
    {
        await using var db = await CreateCleanContext();
        var (admin, _, tenant) = await SeedTestData(db);

        var request = new AddTenantMemberRequest(admin.Id);

        var result = await CallAddTenantMember(db, tenant.Id, request);

        result.Should().BeOfType<Created>();
    }

    [Fact]
    public async Task AddTenantMember_AlreadyMember_Returns409()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var request = new AddTenantMemberRequest(regularUser.Id);

        var result = await CallAddTenantMember(db, tenant.Id, request);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task RemoveTenantMember_Success()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var result = await CallRemoveTenantMember(db, tenant.Id, regularUser.Id);

        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task RemoveTenantMember_NotFound_Returns404()
    {
        await using var db = await CreateCleanContext();
        var (_, _, tenant) = await SeedTestData(db);

        var result = await CallRemoveTenantMember(db, tenant.Id, Guid.NewGuid());

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task GetMyTenants_ReturnsUserActiveTenants()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);

        var result = await CallGetMyTenants(db, principal);

        result.Should().BeOfType<Ok<List<UserTenantResponse>>>();
        var okResult = (Ok<List<UserTenantResponse>>)result;
        okResult.Value!.Count.Should().Be(1);
        okResult.Value.First().TenantSlug.Should().Be("test-tenant");
    }

    [Fact]
    public async Task SetActiveTenant_Member_ReturnsTenantInfo()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);
        var request = new SetActiveTenantRequest(tenant.Id);

        var result = await CallSetActiveTenant(db, request, principal);

        result.Should().BeOfType<Ok<UserTenantResponse>>();
    }

    [Fact]
    public async Task SetActiveTenant_NonMember_ReturnsForbid()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, _) = await SeedTestData(db);

        var otherTenant = new Tenant { Id = Guid.NewGuid(), Name = "Other", Slug = "other", IsActive = true };
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);
        var request = new SetActiveTenantRequest(otherTenant.Id);

        var result = await CallSetActiveTenant(db, request, principal);

        result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task GetMyTenants_AfterSeed_AdminSeesWarpTenant()
    {
        // This tests the exact production flow: DbInitializer seeds the admin user
        // and Warp tenant with membership, then GetMyTenants should return it.
        await using var db = await CreateCleanContext();

        // Simulate what DbInitializer does: create tenant, user, and membership
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Warp",
            Slug = "warp",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Michael",
            LastName = "Schmidt",
            Email = "mikenging@hotmail.com",
            Role = UserRole.SystemAdministrator,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = admin.Id,
            TenantId = tenant.Id,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Now simulate what happens when the user logs in via Keycloak:
        // Keycloak provides a 'sub' claim (that won't match DB yet since no KeycloakSubjectId)
        // and an 'email' claim that SHOULD match.
        var principal = TestHelpers.CreateAdminPrincipal(
            sub: "keycloak-sub-that-wont-match",
            email: "mikenging@hotmail.com");

        var result = await CallGetMyTenants(db, principal);

        result.Should().BeOfType<Ok<List<UserTenantResponse>>>();
        var okResult = (Ok<List<UserTenantResponse>>)result;
        okResult.Value.Should().NotBeNullOrEmpty("admin should see at least one tenant after seeding");
        okResult.Value!.Count.Should().Be(1);
        okResult.Value.First().TenantName.Should().Be("Warp");
        okResult.Value.First().TenantSlug.Should().Be("warp");
    }

    [Fact]
    public async Task GetMyTenants_UserWithNoTenants_ReturnsEmptyList()
    {
        await using var db = await CreateCleanContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = "orphan-sub-id",
            FirstName = "Orphan",
            LastName = "User",
            Email = "orphan@example.com",
            Role = UserRole.User,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal(user.Email, user.KeycloakSubjectId!);

        var result = await CallGetMyTenants(db, principal);

        result.Should().BeOfType<Ok<List<UserTenantResponse>>>();
        var okResult = (Ok<List<UserTenantResponse>>)result;
        okResult.Value.Should().BeEmpty("user with no memberships should see no tenants");
    }

    [Fact]
    public async Task GetMyTenants_UserNotInDb_ReturnsNotFound()
    {
        await using var db = await CreateCleanContext();

        // User exists in Keycloak but not in our DB at all
        var principal = TestHelpers.CreateUserPrincipal("unknown@example.com", "unknown-sub-id");

        var result = await CallGetMyTenants(db, principal);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetMyTenants_InactiveTenant_NotReturned()
    {
        await using var db = await CreateCleanContext();

        var inactiveTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Corp",
            Slug = "inactive-corp",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Tenants.Add(inactiveTenant);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = "inactive-test-sub",
            FirstName = "Test",
            LastName = "User",
            Email = "test@inactive.com",
            Role = UserRole.User,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = inactiveTenant.Id,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var principal = TestHelpers.CreateUserPrincipal(user.Email, user.KeycloakSubjectId!);

        var result = await CallGetMyTenants(db, principal);

        result.Should().BeOfType<Ok<List<UserTenantResponse>>>();
        var okResult = (Ok<List<UserTenantResponse>>)result;
        okResult.Value.Should().BeEmpty("inactive tenants should not appear in the list");
    }

    // Helper methods to invoke endpoint logic directly using reflection
    // The endpoint methods are private static, so we use reflection to test them

    private static int? GetStatusCode(IResult result)
    {
        var prop = result.GetType().GetProperty("StatusCode");
        return prop?.GetValue(result) as int?;
    }

    private static async Task<IResult> CallGetAllTenants(WarpBusinessDbContext db, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(TenantEndpoints).GetMethod("GetAllTenants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [principal, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetTenantById(WarpBusinessDbContext db, Guid id, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(TenantEndpoints).GetMethod("GetTenantById",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, principal, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallCreateTenant(WarpBusinessDbContext db, CreateTenantRequest request)
    {
        var method = typeof(TenantEndpoints).GetMethod("CreateTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [request, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallUpdateTenant(WarpBusinessDbContext db, Guid id, UpdateTenantRequest request)
    {
        var method = typeof(TenantEndpoints).GetMethod("UpdateTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, request, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallDeleteTenant(WarpBusinessDbContext db, Guid id)
    {
        var method = typeof(TenantEndpoints).GetMethod("DeleteTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetTenantMembers(WarpBusinessDbContext db, Guid id, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(TenantEndpoints).GetMethod("GetTenantMembers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, principal, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallAddTenantMember(WarpBusinessDbContext db, Guid tenantId, AddTenantMemberRequest request)
    {
        var method = typeof(TenantEndpoints).GetMethod("AddTenantMember",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [tenantId, request, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallRemoveTenantMember(WarpBusinessDbContext db, Guid tenantId, Guid userId)
    {
        var method = typeof(TenantEndpoints).GetMethod("RemoveTenantMember",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [tenantId, userId, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetMyTenants(WarpBusinessDbContext db, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(TenantEndpoints).GetMethod("GetMyTenants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [principal, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallSetActiveTenant(WarpBusinessDbContext db, SetActiveTenantRequest request, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(TenantEndpoints).GetMethod("SetActiveTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [request, principal, db, CancellationToken.None])!;
    }
}
