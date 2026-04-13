using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Endpoints;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;
using WarpBusiness.Employees.Data;

namespace WarpBusiness.Api.Tests.Endpoints;

[Collection("Database")]
public class UserEndpointTests
{
    private readonly PostgreSqlFixture _fixture;

    public UserEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<WarpBusinessDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();
        return db;
    }

    private async Task<EmployeeDbContext> CreateCleanEmployeeContext()
    {
        var db = TestHelpers.CreatePostgresEmployeeDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureEmployeeSchemaAsync(db);
        db.Employees.RemoveRange(db.Employees);
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

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = regularUser.Id,
            TenantId = tenant.Id,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return (admin, regularUser, tenant);
    }

    private static KeycloakAdminService CreateFakeKeycloakAdmin(FakeHttpMessageHandler? handler = null)
    {
        handler ??= new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:AdminUser"] = "admin",
                ["Keycloak:AdminPassword"] = "admin"
            })
            .Build();
        return new KeycloakAdminService(httpClient, config, NullLogger<KeycloakAdminService>.Instance);
    }

    [Fact]
    public async Task GetCurrentUser_MatchesBySub_ReturnsUser()
    {
        await using var db = await CreateCleanContext();
        var (admin, _, _) = await SeedTestData(db);

        var principal = TestHelpers.CreateAdminPrincipal(sub: admin.KeycloakSubjectId, email: admin.Email);

        var result = await CallGetCurrentUser(db, principal);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.Email.Should().Be("admin@warpindustries.com");
    }

    [Fact]
    public async Task GetCurrentUser_MatchesByEmail_LinksKeycloakSubjectId()
    {
        await using var db = await CreateCleanContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Unlinked",
            LastName = "User",
            Email = "unlinked@warpindustries.com",
            Role = UserRole.User
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var newSubId = "new-keycloak-sub-id";
        var principal = TestHelpers.CreateUserPrincipal(email: "unlinked@warpindustries.com", sub: newSubId);

        var result = await CallGetCurrentUser(db, principal);

        result.Should().BeOfType<Ok<UserResponse>>();

        await using var verifyDb = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        var updated = await verifyDb.Users.FirstOrDefaultAsync(u => u.Email == "unlinked@warpindustries.com");
        updated!.KeycloakSubjectId.Should().Be(newSubId);
    }

    [Fact]
    public async Task GetCurrentUser_NoMatch_Returns404()
    {
        await using var db = await CreateCleanContext();

        var principal = TestHelpers.CreateUserPrincipal(email: "nobody@warpindustries.com", sub: "nobody-sub");

        var result = await CallGetCurrentUser(db, principal);

        // Results.NotFound(new { message = "..." }) returns NotFound<anonymous>, check via status code
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetAllUsers_Admin_NoTenantContext_ReturnsAll()
    {
        await using var db = await CreateCleanContext();
        await using var empDb = await CreateCleanEmployeeContext();
        await SeedTestData(db);

        var principal = TestHelpers.CreateAdminPrincipal();
        var httpContext = CreateHttpContext(principal, tenantId: null);

        var result = await CallGetAllUsers(db, empDb, principal, httpContext);

        result.Should().BeOfType<Ok<List<UserResponse>>>();
        var okResult = (Ok<List<UserResponse>>)result;
        okResult.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetAllUsers_Admin_WithTenantContext_ReturnsTenantMembers()
    {
        await using var db = await CreateCleanContext();
        await using var empDb = await CreateCleanEmployeeContext();
        var (_, regularUser, tenant) = await SeedTestData(db);

        var principal = TestHelpers.CreateAdminPrincipal();
        var httpContext = CreateHttpContext(principal, tenantId: tenant.Id);

        var result = await CallGetAllUsers(db, empDb, principal, httpContext);

        result.Should().BeOfType<Ok<List<UserResponse>>>();
        var okResult = (Ok<List<UserResponse>>)result;
        okResult.Value!.Count.Should().Be(1);
        okResult.Value.First().Email.Should().Be("user@warpindustries.com");
    }

    [Fact]
    public async Task CreateUser_Success_WithMockedKeycloak()
    {
        await using var db = await CreateCleanContext();

        var handler = new FakeHttpMessageHandler();
        var keycloakId = Guid.NewGuid().ToString();
        handler.QueueCreateUserResponse(keycloakId);
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var request = new CreateUserRequest("New", "Person", "new@warpindustries.com", "Password1!", UserRole.User);

        var result = await CallCreateUser(db, request, keycloakAdmin);

        result.Should().BeOfType<Created<UserResponse>>();
        var created = (Created<UserResponse>)result;
        created.Value!.Email.Should().Be("new@warpindustries.com");
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        await using var db = await CreateCleanContext();
        await SeedTestData(db);

        var keycloakAdmin = CreateFakeKeycloakAdmin();

        var request = new CreateUserRequest("Dup", "User", "admin@warpindustries.com", "Password1!", UserRole.User);

        var result = await CallCreateUser(db, request, keycloakAdmin);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateUser_Success()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, _) = await SeedTestData(db);

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var request = new UpdateUserRequest("Updated", "Name", "user@warpindustries.com", UserRole.User);

        var result = await CallUpdateUser(db, regularUser.Id, request, keycloakAdmin);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_DuplicateEmail_Returns409()
    {
        await using var db = await CreateCleanContext();
        var (admin, regularUser, _) = await SeedTestData(db);

        var keycloakAdmin = CreateFakeKeycloakAdmin();

        var request = new UpdateUserRequest("Regular", "User", "admin@warpindustries.com", UserRole.User);

        var result = await CallUpdateUser(db, regularUser.Id, request, keycloakAdmin);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task DeleteUser_Success_CascadesMemberships()
    {
        await using var db = await CreateCleanContext();
        await using var empDb = await CreateCleanEmployeeContext();
        var (_, regularUser, _) = await SeedTestData(db);
        var userId = regularUser.Id;

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var result = await CallDeleteUser(db, empDb, regularUser.Id, keycloakAdmin);

        result.Should().BeOfType<NoContent>();

        var memberships = await db.UserTenantMemberships
            .Where(m => m.UserId == userId)
            .ToListAsync();
        memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUser_NotFound_Returns404()
    {
        await using var db = await CreateCleanContext();
        await using var empDb = await CreateCleanEmployeeContext();

        var keycloakAdmin = CreateFakeKeycloakAdmin();

        var result = await CallDeleteUser(db, empDb, Guid.NewGuid(), keycloakAdmin);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task GetUserById_Found_ReturnsUser()
    {
        await using var db = await CreateCleanContext();
        var (admin, _, _) = await SeedTestData(db);

        var result = await CallGetUserById(db, admin.Id);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.Email.Should().Be("admin@warpindustries.com");
    }

    [Fact]
    public async Task GetUserById_NotFound_Returns404()
    {
        await using var db = await CreateCleanContext();

        var result = await CallGetUserById(db, Guid.NewGuid());

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateMyProfile_ValidRequest_UpdatesName()
    {
        await using var db = await CreateCleanContext();
        var (_, regularUser, _) = await SeedTestData(db);

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var principal = TestHelpers.CreateUserPrincipal(regularUser.Email, regularUser.KeycloakSubjectId);
        var request = new UpdateProfileRequest("NewFirst", "NewLast");

        var result = await CallUpdateMyProfile(db, principal, request, keycloakAdmin);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.FirstName.Should().Be("NewFirst");
        okResult.Value!.LastName.Should().Be("NewLast");
    }

    [Fact]
    public async Task UpdateMyProfile_UserNotInDb_ReturnsNotFound()
    {
        await using var db = await CreateCleanContext();

        var keycloakAdmin = CreateFakeKeycloakAdmin();
        var principal = TestHelpers.CreateUserPrincipal("ghost@nowhere.com", "ghost-sub-id");
        var request = new UpdateProfileRequest("Ghost", "User");

        var result = await CallUpdateMyProfile(db, principal, request, keycloakAdmin);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateMyProfile_ViaEmailFallback_UpdatesName()
    {
        await using var db = await CreateCleanContext();

        // Seed a user with no KeycloakSubjectId (like the seeded admin before first login)
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Michael",
            LastName = "Schmidt",
            Email = "mikenging@hotmail.com",
            Role = UserRole.SystemAdministrator,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var keycloakAdmin = CreateFakeKeycloakAdmin();

        // Principal sub won't match any DB user, but email will
        var principal = TestHelpers.CreateAdminPrincipal(
            sub: "keycloak-sub-that-wont-match",
            email: "mikenging@hotmail.com");
        var request = new UpdateProfileRequest("Mike", "Schmidt");

        var result = await CallUpdateMyProfile(db, principal, request, keycloakAdmin);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.FirstName.Should().Be("Mike");
        okResult.Value!.LastName.Should().Be("Schmidt");
    }

    [Fact]
    public async Task UpdateMyProfile_PreservesEmailAndRole()
    {
        await using var db = await CreateCleanContext();
        var (admin, _, _) = await SeedTestData(db);

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var principal = TestHelpers.CreateAdminPrincipal(sub: admin.KeycloakSubjectId, email: admin.Email);
        var request = new UpdateProfileRequest("Changed", "Name");

        var result = await CallUpdateMyProfile(db, principal, request, keycloakAdmin);

        result.Should().BeOfType<Ok<UserResponse>>();
        var okResult = (Ok<UserResponse>)result;
        okResult.Value!.FirstName.Should().Be("Changed");
        okResult.Value!.LastName.Should().Be("Name");
        // Email and role must remain unchanged
        okResult.Value!.Email.Should().Be("admin@warpindustries.com");
        okResult.Value!.Role.Should().Be(UserRole.SystemAdministrator);
    }

    // Helper methods

    private static int? GetStatusCode(IResult result)
    {
        // Use reflection to get StatusCode from typed results with anonymous types
        var prop = result.GetType().GetProperty("StatusCode");
        return prop?.GetValue(result) as int?;
    }

    private static HttpContext CreateHttpContext(System.Security.Claims.ClaimsPrincipal principal, Guid? tenantId)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        if (tenantId.HasValue)
            httpContext.Items["TenantId"] = tenantId.Value;
        return httpContext;
    }

    private static async Task<IResult> CallGetCurrentUser(WarpBusinessDbContext db, System.Security.Claims.ClaimsPrincipal principal)
    {
        var method = typeof(UserEndpoints).GetMethod("GetCurrentUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [principal, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetAllUsers(WarpBusinessDbContext db, EmployeeDbContext empDb, System.Security.Claims.ClaimsPrincipal principal, HttpContext httpContext)
    {
        var method = typeof(UserEndpoints).GetMethod("GetAllUsers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [db, empDb, principal, httpContext, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetUserById(WarpBusinessDbContext db, Guid id)
    {
        var method = typeof(UserEndpoints).GetMethod("GetUserById",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallCreateUser(WarpBusinessDbContext db, CreateUserRequest request, KeycloakAdminService keycloakAdmin)
    {
        var method = typeof(UserEndpoints).GetMethod("CreateUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var logger = NullLogger<KeycloakAdminService>.Instance;
        return await (Task<IResult>)method.Invoke(null, [request, db, keycloakAdmin, logger, CancellationToken.None])!;
    }

    private static async Task<IResult> CallUpdateUser(WarpBusinessDbContext db, Guid id, UpdateUserRequest request, KeycloakAdminService keycloakAdmin)
    {
        var method = typeof(UserEndpoints).GetMethod("UpdateUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, request, db, keycloakAdmin, CancellationToken.None])!;
    }

    private static async Task<IResult> CallDeleteUser(WarpBusinessDbContext db, EmployeeDbContext empDb, Guid id, KeycloakAdminService keycloakAdmin)
    {
        var method = typeof(UserEndpoints).GetMethod("DeleteUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, db, empDb, keycloakAdmin, CancellationToken.None])!;
    }

    private static async Task<IResult> CallUpdateMyProfile(
        WarpBusinessDbContext db,
        System.Security.Claims.ClaimsPrincipal principal,
        UpdateProfileRequest request,
        KeycloakAdminService keycloakAdmin)
    {
        var method = typeof(UserEndpoints).GetMethod("UpdateMyProfile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [principal, request, db, keycloakAdmin, CancellationToken.None])!;
    }
}
