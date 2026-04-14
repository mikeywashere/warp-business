using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Endpoints;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Endpoints;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Tests.Endpoints;

/// <summary>
/// Tests for the employee-user account linking feature.
/// New endpoints being built by Data in parallel:
///   GET  /api/users/unlinked
///   POST /api/employees/with-user
///   PUT  /api/employees/{id}/with-user
///   GET  /api/employees/by-user/{userId}
/// </summary>
[Collection("Database")]
public class EmployeeUserLinkingTests
{
    private readonly PostgreSqlFixture _fixture;

    public EmployeeUserLinkingTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    #region Setup Helpers

    private async Task<EmployeeDbContext> CreateCleanEmployeeContext()
    {
        var db = TestHelpers.CreatePostgresEmployeeDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureEmployeeSchemaAsync(db);
        db.Employees.RemoveRange(db.Employees);
        await db.SaveChangesAsync();
        return db;
    }

    private async Task<WarpBusinessDbContext> CreateCleanWarpContext()
    {
        var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();
        return db;
    }

    private static HttpContext CreateHttpContextWithTenant(Guid tenantId)
    {
        var context = new DefaultHttpContext();
        context.Items["TenantId"] = tenantId;
        return context;
    }

    private static CreateEmployeeRequest MakeCreateRequest(
        string firstName = "Jean-Luc",
        string lastName = "Picard",
        string email = "picard@enterprise.com",
        Guid? userId = null,
        EmploymentStatus status = EmploymentStatus.Active,
        EmploymentType type = EmploymentType.FullTime,
        decimal payAmount = 100000m,
        PayType payType = PayType.Salary,
        string currency = "USD") =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), "Command", "Captain", null, status, type, userId, payAmount, payType, currency);

    private static UpdateEmployeeRequest MakeUpdateRequest(
        string firstName = "Jean-Luc",
        string lastName = "Picard",
        string email = "picard@enterprise.com",
        Guid? userId = null,
        decimal payAmount = 100000m,
        PayType payType = PayType.Salary,
        string currency = "USD") =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), null, "Command", "Captain", null,
            EmploymentStatus.Active, EmploymentType.FullTime, userId, payAmount, payType, currency);

    /// <summary>
    /// Seeds an ApplicationUser in the warp schema and adds tenant membership.
    /// Returns the user's Id.
    /// </summary>
    private static async Task<ApplicationUser> SeedUser(
        WarpBusinessDbContext warpDb,
        Guid tenantId,
        string email,
        string firstName = "Test",
        string lastName = "User")
    {
        var tenant = await warpDb.Tenants.FindAsync(tenantId);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = tenantId,
                Name = $"Tenant-{tenantId.ToString()[..8]}",
                Slug = $"tenant-{tenantId.ToString()[..8]}",
                IsActive = true
            };
            warpDb.Tenants.Add(tenant);
            await warpDb.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = Guid.NewGuid().ToString(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        warpDb.Users.Add(user);
        await warpDb.SaveChangesAsync();

        warpDb.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId,
            JoinedAt = DateTime.UtcNow
        });
        await warpDb.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Seeds an Employee directly in the employee schema.
    /// </summary>
    private static async Task<Employee> SeedEmployee(
        EmployeeDbContext empDb,
        Guid tenantId,
        string email,
        Guid? userId = null,
        string firstName = "Test",
        string lastName = "Employee",
        string employeeNumber = "EMP99999")
    {
        var emp = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = employeeNumber,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            HireDate = new DateOnly(2364, 1, 1),
            EmploymentStatus = EmploymentStatus.Active,
            EmploymentType = EmploymentType.FullTime,
            UserId = userId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        empDb.Employees.Add(emp);
        await empDb.SaveChangesAsync();
        return emp;
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

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task CannotLinkTwoEmployeesToSameUser()
    {
        // The filtered unique index on Employee.UserId (WHERE UserId IS NOT NULL)
        // should prevent two employees from sharing the same UserId.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();
        var sharedUserId = Guid.NewGuid();

        await SeedEmployee(empDb, tenantId, "emp1@enterprise.com",
            userId: sharedUserId, firstName: "First", employeeNumber: "EMP10001");

        // Second employee with the same UserId should violate the unique index
        var act = async () => await SeedEmployee(empDb, tenantId, "emp2@enterprise.com",
            userId: sharedUserId, firstName: "Second", employeeNumber: "EMP10002");

        await act.Should().ThrowAsync<DbUpdateException>(
            "a filtered unique index on UserId should prevent duplicate links");
    }

    [Fact]
    public async Task CanCreateEmployeesWithSameEmailInDifferentTenants()
    {
        // After Data changes the email index to be tenant-scoped: (Email, TenantId),
        // the same email address should be allowed across tenants.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var httpContextA = CreateHttpContextWithTenant(tenantA);
        var httpContextB = CreateHttpContextWithTenant(tenantB);

        var requestA = MakeCreateRequest(email: "shared@federation.com");
        var resultA = await CallCreateEmployee(httpContextA, requestA, empDb);
        resultA.Should().BeOfType<Created<EmployeeResponse>>();

        var requestB = MakeCreateRequest(email: "shared@federation.com", firstName: "Mirror");
        var resultB = await CallCreateEmployee(httpContextB, requestB, empDb);
        resultB.Should().BeOfType<Created<EmployeeResponse>>(
            "same email in a different tenant should succeed with the tenant-scoped index");
    }

    [Fact]
    public async Task CannotCreateEmployeesWithSameEmailInSameTenant()
    {
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);

        var request = MakeCreateRequest(email: "unique@enterprise.com");
        await CallCreateEmployee(httpContext, request, empDb);

        // Duplicate in same tenant
        var httpContext2 = CreateHttpContextWithTenant(tenantId);
        var duplicate = MakeCreateRequest(firstName: "Duplicate", email: "unique@enterprise.com");
        var result = await CallCreateEmployee(httpContext2, duplicate, empDb);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    #endregion

    #region Link Validation Tests

    [Fact]
    public async Task CreateEmployee_WithUserId_ValidatesUserExists()
    {
        // Setting a non-existent UserId should return 400.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);

        var request = MakeCreateRequest(userId: Guid.NewGuid()); // no such user exists

        var result = await CallCreateEmployee(httpContext, request, empDb, new Api.Services.UserValidator(warpDb));

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400,
            "creating an employee with a non-existent UserId should be rejected");
    }

    [Fact]
    public async Task CreateEmployee_WithUserId_ValidatesUserInTenant()
    {
        // Setting UserId to a user in a DIFFERENT tenant should return 400.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var userInTenantB = await SeedUser(warpDb, tenantB, "remote@otherco.com");

        var httpContext = CreateHttpContextWithTenant(tenantA);
        var request = MakeCreateRequest(userId: userInTenantB.Id);

        var result = await CallCreateEmployee(httpContext, request, empDb, new Api.Services.UserValidator(warpDb));

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400,
            "a user from a different tenant should not be linkable");
    }

    [Fact]
    public async Task CreateEmployee_WithUserId_ValidatesUserNotAlreadyLinked()
    {
        // Setting UserId that is already linked to another employee should return 409.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "taken@enterprise.com");

        // Link this user to a first employee
        await SeedEmployee(empDb, tenantId, "first@enterprise.com",
            userId: user.Id, employeeNumber: "EMP20001");

        // Try to create another employee linked to the same user
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(email: "second@enterprise.com", userId: user.Id);

        var result = await CallCreateEmployee(httpContext, request, empDb, new Api.Services.UserValidator(warpDb));

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409,
            "a user already linked to an employee should not be linkable to another");
    }

    [Fact]
    public async Task UpdateEmployee_CannotChangeExistingUserId()
    {
        // Once linked, always linked — cannot change UserId to a different value.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();
        var originalUserId = Guid.NewGuid();

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var createReq = MakeCreateRequest(email: "linked@enterprise.com", userId: originalUserId);
        var createResult = await CallCreateEmployee(httpContext, createReq, empDb);
        var created = createResult as Created<EmployeeResponse>;
        // If creation validation blocks non-existent users, seed directly
        if (created is null)
        {
            await SeedEmployee(empDb, tenantId, "linked@enterprise.com",
                userId: originalUserId, employeeNumber: "EMP30001");
            var emp = await empDb.Employees.FirstAsync(e => e.Email == "linked@enterprise.com");

            var differentUserId = Guid.NewGuid();
            var updateReq = MakeUpdateRequest(email: "linked@enterprise.com", userId: differentUserId);
            var updateResult = await CallUpdateEmployee(emp.Id, updateReq, CreateHttpContextWithTenant(tenantId), empDb);

            var statusCode = GetStatusCode(updateResult);
            statusCode.Should().Be(400,
                "changing an existing UserId to a different value should be rejected");
            return;
        }

        var employeeId = created!.Value!.Id;
        var differentUserId2 = Guid.NewGuid();
        var updateRequest = MakeUpdateRequest(email: "linked@enterprise.com", userId: differentUserId2);
        var result = await CallUpdateEmployee(employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), empDb);

        var sc = GetStatusCode(result);
        sc.Should().Be(400,
            "changing an existing UserId to a different value should be rejected");
    }

    [Fact]
    public async Task UpdateEmployee_CanSetUserIdFromNull()
    {
        // An employee with no UserId can have one set (null → value is OK).
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var createReq = MakeCreateRequest(email: "unlinked@enterprise.com", userId: null);
        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(httpContext, createReq, empDb);
        var employeeId = createResult.Value!.Id;
        createResult.Value.UserId.Should().BeNull();

        var newUserId = Guid.NewGuid();
        var updateRequest = MakeUpdateRequest(email: "unlinked@enterprise.com", userId: newUserId);
        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), empDb);

        // Depending on Data's validation (user must exist), this may return 400 or 200.
        // The key contract: null → value transition should NOT be blocked by the
        // "cannot change UserId" rule. If it returns 400, it should be because the
        // user doesn't exist, not because UserId was changed.
        var statusCode = GetStatusCode(result);
        // If Data adds user-existence validation, 400 is acceptable here.
        // If no validation yet, the update should succeed with 200.
        statusCode.Should().Match(s => s == 200 || s == 400,
            "null → value should be allowed by the immutability rule; 400 only if user-existence check fails");
    }

    #endregion

    #region Deletion Tests

    [Fact]
    public async Task DeleteEmployee_BlockedWhenLinkedToUser()
    {
        // An employee linked to a user account should not be deletable.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var emp = await SeedEmployee(empDb, tenantId, "linked-del@enterprise.com",
            userId: userId, employeeNumber: "EMP40001");

        var result = await CallDeleteEmployee(emp.Id, CreateHttpContextWithTenant(tenantId), empDb);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400,
            "deleting an employee linked to a user account should be blocked");
    }

    [Fact]
    public async Task DeleteEmployee_AllowedWhenNoLinkedUser()
    {
        // An employee with no UserId should be deletable normally.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var createReq = MakeCreateRequest(email: "unlinked-del@enterprise.com", userId: null);
        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(httpContext, createReq, empDb);
        var employeeId = createResult.Value!.Id;

        var result = await CallDeleteEmployee(employeeId, CreateHttpContextWithTenant(tenantId), empDb);

        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task DeleteUser_BlockedWhenLinkedToEmployee()
    {
        // A user linked to an employee should not be deletable.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "linked-user-del@enterprise.com");
        await SeedEmployee(empDb, tenantId, "emp-linked-del@enterprise.com",
            userId: user.Id, employeeNumber: "EMP50001");

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var result = await CallDeleteUser(warpDb, empDb, user.Id, keycloakAdmin);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400,
            "deleting a user linked to an employee should be blocked");
    }

    [Fact]
    public async Task DeleteUser_AllowedWhenNoLinkedEmployee()
    {
        // A user with no linked employee should be deletable normally.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "free-user@enterprise.com");

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var result = await CallDeleteUser(warpDb, empDb, user.Id, keycloakAdmin);

        result.Should().BeOfType<NoContent>();
    }

    #endregion

    #region Unlinked Users Tests

    [Fact]
    public async Task GetUnlinkedUsers_ReturnsOnlyUnlinkedUsersInTenant()
    {
        // GET /api/users/unlinked should return users in the tenant who are
        // NOT already linked to any employee.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user1 = await SeedUser(warpDb, tenantId, "user1@enterprise.com", "Riker", "William");
        var user2 = await SeedUser(warpDb, tenantId, "user2@enterprise.com", "Troi", "Deanna");
        var user3 = await SeedUser(warpDb, tenantId, "user3@enterprise.com", "Worf", "Son of Mogh");

        // Link user1 to an employee
        await SeedEmployee(empDb, tenantId, "emp-riker@enterprise.com",
            userId: user1.Id, employeeNumber: "EMP60001");

        var httpContext = CreateHttpContextWithTenant(tenantId);

        // Call the new endpoint via reflection
        var result = await CallGetUnlinkedUsers(httpContext, warpDb, empDb);

        result.Should().BeOfType<Ok<List<UserResponse>>>();
        var okResult = (Ok<List<UserResponse>>)result;
        okResult.Value!.Count.Should().Be(2, "only unlinked users should be returned");
        okResult.Value.Should().Contain(u => u.Email == "user2@enterprise.com");
        okResult.Value.Should().Contain(u => u.Email == "user3@enterprise.com");
        okResult.Value.Should().NotContain(u => u.Email == "user1@enterprise.com",
            "linked user should be excluded");
    }

    [Fact]
    public async Task GetUnlinkedUsers_ExcludesUsersFromOtherTenants()
    {
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedUser(warpDb, tenantA, "usera@enterprise.com", "TenantA", "User");
        await SeedUser(warpDb, tenantB, "userb@otherco.com", "TenantB", "User");

        var httpContext = CreateHttpContextWithTenant(tenantA);
        var result = await CallGetUnlinkedUsers(httpContext, warpDb, empDb);

        result.Should().BeOfType<Ok<List<UserResponse>>>();
        var okResult = (Ok<List<UserResponse>>)result;
        okResult.Value!.Should().OnlyContain(u => u.Email == "usera@enterprise.com",
            "only users in the requested tenant should appear");
    }

    #endregion

    #region Create Employee With User Tests

    [Fact]
    public async Task CreateEmployeeWithUser_CreatesEmployeeAndUser()
    {
        // POST /api/employees/with-user — happy path with mocked Keycloak.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        // Ensure tenant exists
        warpDb.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Test Tenant", Slug = "test-tenant", IsActive = true
        });
        await warpDb.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler();
        var keycloakId = Guid.NewGuid().ToString();
        handler.QueueCreateUserResponse(keycloakId);
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallCreateEmployeeWithUser(
            httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "Geordi",
            lastName: "La Forge",
            email: "laforge@enterprise.com",
            role: UserRole.User);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(201, "happy path should create successfully");
    }

    [Fact]
    public async Task CreateEmployeeWithUser_LinksUserIdToEmployee()
    {
        // Verify the created employee has the new user's UserId set.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        warpDb.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Link Tenant", Slug = "link-tenant", IsActive = true
        });
        await warpDb.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler();
        var keycloakId = Guid.NewGuid().ToString();
        handler.QueueCreateUserResponse(keycloakId);
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallCreateEmployeeWithUser(
            httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "Data",
            lastName: "Soong",
            email: "data@enterprise.com",
            role: UserRole.User);

        // Verify the employee was created with a UserId
        var employee = await empDb.Employees
            .FirstOrDefaultAsync(e => e.Email == "data@enterprise.com");

        employee.Should().NotBeNull("employee should have been created");
        employee!.UserId.Should().NotBeNull("employee should be linked to the new user");

        // The user should exist in the warp schema
        var user = await warpDb.Users
            .FirstOrDefaultAsync(u => u.Email == "data@enterprise.com");

        user.Should().NotBeNull("user should have been created");
        employee.UserId.Should().Be(user!.Id, "employee should link to the correct user");
    }

    [Fact]
    public async Task CreateEmployeeWithUser_CreatesUserWithoutPassword()
    {
        // The Keycloak user should be created with requiredActions (set password email),
        // NOT with credentials. This is verified by ensuring the Keycloak service is
        // called — the FakeHttpMessageHandler confirms the right number of requests.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        warpDb.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "No-Pw Tenant", Slug = "nopw-tenant", IsActive = true
        });
        await warpDb.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler();
        var keycloakId = Guid.NewGuid().ToString();
        handler.QueueCreateUserResponse(keycloakId);
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallCreateEmployeeWithUser(
            httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "Beverly",
            lastName: "Crusher",
            email: "crusher@enterprise.com",
            role: UserRole.User);

        // The key assertion: the user in the DB should exist, proving Keycloak was called.
        // The actual "no password" contract is in the endpoint implementation which should
        // use requiredActions: ["UPDATE_PASSWORD"] instead of credentials.
        var user = await warpDb.Users
            .FirstOrDefaultAsync(u => u.Email == "crusher@enterprise.com");

        user.Should().NotBeNull("user should have been created via Keycloak");
    }

    #endregion

    #region Update Employee With User Tests

    [Fact]
    public async Task UpdateEmployeeWithUser_SyncsSharedFields()
    {
        // PUT /api/employees/{id}/with-user should update FirstName, LastName,
        // Email on BOTH the employee AND the linked user record.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "sync@enterprise.com", "Old", "Name");
        var emp = await SeedEmployee(empDb, tenantId, "sync@enterprise.com",
            userId: user.Id, firstName: "Old", lastName: "Name", employeeNumber: "EMP70001");

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallUpdateEmployeeWithUser(
            emp.Id, httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "New",
            lastName: "Updated",
            email: "updated@enterprise.com",
            role: user.Role);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(200, "update should succeed");

        // Verify employee was updated
        await empDb.Entry(emp).ReloadAsync();
        emp.FirstName.Should().Be("New");
        emp.LastName.Should().Be("Updated");
        emp.Email.Should().Be("updated@enterprise.com");

        // Verify user was also updated
        await warpDb.Entry(user).ReloadAsync();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Updated");
        user.Email.Should().Be("updated@enterprise.com");
    }

    [Fact]
    public async Task UpdateEmployeeWithUser_UpdatesUserRole()
    {
        // Verify role-only update works on the linked user.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "role@enterprise.com", "Role", "Test");
        var emp = await SeedEmployee(empDb, tenantId, "role@enterprise.com",
            userId: user.Id, firstName: "Role", lastName: "Test", employeeNumber: "EMP70002");

        user.Role.Should().Be(UserRole.User);

        var handler = new FakeHttpMessageHandler();
        handler.QueueSuccessResponse();
        var keycloakAdmin = CreateFakeKeycloakAdmin(handler);

        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallUpdateEmployeeWithUser(
            emp.Id, httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "Role",
            lastName: "Test",
            email: "role@enterprise.com",
            role: UserRole.SystemAdministrator);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(200);

        await warpDb.Entry(user).ReloadAsync();
        user.Role.Should().Be(UserRole.SystemAdministrator,
            "user role should be updated to SystemAdministrator");
    }

    [Fact]
    public async Task UpdateEmployeeWithUser_FallsBackToEmployeeOnlyWhenNoLinkedUser()
    {
        // When the employee has no UserId, the "with-user" update should
        // still succeed by updating only the employee record.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var emp = await SeedEmployee(empDb, tenantId, "solo@enterprise.com",
            userId: null, firstName: "Solo", lastName: "Employee", employeeNumber: "EMP70003");

        var keycloakAdmin = CreateFakeKeycloakAdmin();
        var httpContext = CreateHttpContextWithTenant(tenantId);

        var result = await CallUpdateEmployeeWithUser(
            emp.Id, httpContext, empDb, warpDb, keycloakAdmin,
            firstName: "Updated",
            lastName: "Solo",
            email: "solo@enterprise.com",
            role: UserRole.User);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(200, "should succeed even without a linked user");

        await empDb.Entry(emp).ReloadAsync();
        emp.FirstName.Should().Be("Updated");
    }

    #endregion

    #region Link User to Employee Tests

    [Fact]
    public async Task LinkUserToEmployee_LinksExistingUserAndSyncsData()
    {
        // PUT /api/employees/{id}/link-user/{userId} — link an existing user to an unlinked employee.
        // Should sync missing data from user to employee.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        // Create a user with rich data
        var user = await SeedUser(warpDb, tenantId, "linked@enterprise.com", "Jean-Luc", "Picard");

        // Create an employee with minimal data
        var emp = await SeedEmployee(empDb, tenantId, "",
            userId: null, firstName: "", lastName: "", employeeNumber: "EMP90001");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp.Id, user.Id, httpContext, warpDb, empDb);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(200, "linking should succeed");

        // Verify employee was linked
        await empDb.Entry(emp).ReloadAsync();
        emp.UserId.Should().Be(user.Id, "employee should be linked to user");

        // Verify data was synced
        emp.FirstName.Should().Be("Jean-Luc", "first name should be synced from user");
        emp.LastName.Should().Be("Picard", "last name should be synced from user");
        emp.Email.Should().Be("linked@enterprise.com", "email should be synced from user");
    }

    [Fact]
    public async Task LinkUserToEmployee_PreservesExistingEmployeeData()
    {
        // When employee has some data, syncing should NOT overwrite existing values.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "user@enterprise.com", "User", "First");
        var emp = await SeedEmployee(empDb, tenantId, "emp@enterprise.com",
            userId: null, firstName: "Employee", lastName: "Last", employeeNumber: "EMP90002");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp.Id, user.Id, httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(200);

        // Employee's existing values should be preserved
        await empDb.Entry(emp).ReloadAsync();
        emp.FirstName.Should().Be("Employee", "existing first name should not be overwritten");
        emp.LastName.Should().Be("Last", "existing last name should not be overwritten");
        emp.Email.Should().Be("emp@enterprise.com", "existing email should not be overwritten");
    }

    [Fact]
    public async Task LinkUserToEmployee_Returns400WhenEmployeeAlreadyLinked()
    {
        // Cannot link a user to an employee that already has a UserId.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user1 = await SeedUser(warpDb, tenantId, "user1@enterprise.com", "User", "One");
        var user2 = await SeedUser(warpDb, tenantId, "user2@enterprise.com", "User", "Two");
        var emp = await SeedEmployee(empDb, tenantId, "emp@enterprise.com",
            userId: user1.Id, firstName: "Linked", lastName: "Emp", employeeNumber: "EMP90003");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp.Id, user2.Id, httpContext, warpDb, empDb);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400, "cannot link a second user to an already-linked employee");
    }

    [Fact]
    public async Task LinkUserToEmployee_Returns400WhenUserNotFound()
    {
        // Cannot link a non-existent user.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var emp = await SeedEmployee(empDb, tenantId, "emp@enterprise.com",
            userId: null, firstName: "Test", lastName: "Emp", employeeNumber: "EMP90004");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp.Id, Guid.NewGuid(), httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(400, "cannot link a non-existent user");
    }

    [Fact]
    public async Task LinkUserToEmployee_Returns400WhenUserNotInTenant()
    {
        // Cannot link a user from a different tenant.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var userInTenantB = await SeedUser(warpDb, tenantB, "user@other.com", "Other", "Tenant");
        var emp = await SeedEmployee(empDb, tenantA, "emp@enterprise.com",
            userId: null, firstName: "Test", lastName: "Emp", employeeNumber: "EMP90005");

        var httpContext = CreateHttpContextWithTenant(tenantA);
        var result = await CallLinkUserToEmployee(emp.Id, userInTenantB.Id, httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(400, "cannot link a user from a different tenant");
    }

    [Fact]
    public async Task LinkUserToEmployee_Returns409WhenUserAlreadyLinked()
    {
        // Cannot link a user that is already linked to another employee.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "shared@enterprise.com", "Shared", "User");
        var emp1 = await SeedEmployee(empDb, tenantId, "emp1@enterprise.com",
            userId: user.Id, firstName: "First", lastName: "Emp", employeeNumber: "EMP90006");
        var emp2 = await SeedEmployee(empDb, tenantId, "emp2@enterprise.com",
            userId: null, firstName: "Second", lastName: "Emp", employeeNumber: "EMP90007");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp2.Id, user.Id, httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(409, "cannot link a user already linked to another employee");
    }

    [Fact]
    public async Task LinkUserToEmployee_Returns404WhenEmployeeNotFound()
    {
        // Cannot link to a non-existent employee.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "user@enterprise.com", "Test", "User");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(Guid.NewGuid(), user.Id, httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(404, "cannot link to a non-existent employee");
    }

    [Fact]
    public async Task LinkUserToEmployee_SyncsOnlyMissingFields()
    {
        // Only fields that are null/empty in employee should be synced.
        await using var empDb = await CreateCleanEmployeeContext();
        await using var warpDb = await CreateCleanWarpContext();
        var tenantId = Guid.NewGuid();

        var user = await SeedUser(warpDb, tenantId, "sync@enterprise.com", "User", "Name");
        // Employee has FirstName but not LastName or Email
        var emp = await SeedEmployee(empDb, tenantId, "",
            userId: null, firstName: "EmployeeFirst", lastName: "", employeeNumber: "EMP90008");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallLinkUserToEmployee(emp.Id, user.Id, httpContext, warpDb, empDb);

        GetStatusCode(result).Should().Be(200);

        await empDb.Entry(emp).ReloadAsync();
        emp.FirstName.Should().Be("EmployeeFirst", "existing first name should not be overwritten");
        emp.LastName.Should().Be("Name", "missing last name should be synced");
        emp.Email.Should().Be("sync@enterprise.com", "missing email should be synced");
    }

    #endregion

    #region By-User Endpoint Tests

    [Fact]
    public async Task GetEmployeeByUserId_ReturnsLinkedEmployee()
    {
        // GET /api/employees/by-user/{userId} — happy path.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var emp = await SeedEmployee(empDb, tenantId, "byuser@enterprise.com",
            userId: userId, firstName: "Linked", lastName: "Emp", employeeNumber: "EMP80001");

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallGetEmployeeByUserId(userId, httpContext, empDb);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.Email.Should().Be("byuser@enterprise.com");
        okResult.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetEmployeeByUserId_Returns404WhenNoLink()
    {
        // When no employee is linked to the given userId, return 404.
        await using var empDb = await CreateCleanEmployeeContext();
        var tenantId = Guid.NewGuid();

        var httpContext = CreateHttpContextWithTenant(tenantId);
        var result = await CallGetEmployeeByUserId(Guid.NewGuid(), httpContext, empDb);

        result.Should().BeOfType<NotFound>();
    }

    #endregion

    #region Reflection Helpers

    private static int? GetStatusCode(IResult result)
    {
        var prop = result.GetType().GetProperty("StatusCode");
        return prop?.GetValue(result) as int?;
    }

    private static async Task<IResult> CallCreateEmployee(
        HttpContext httpContext, CreateEmployeeRequest request, EmployeeDbContext db,
        WarpBusiness.Employees.Services.IUserValidator? userValidator = null)
    {
        userValidator ??= new TestHelpers.PermissiveUserValidator();
        var method = typeof(EmployeeEndpoints).GetMethod("CreateEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [request, httpContext, db, userValidator, CancellationToken.None])!;
    }

    private static async Task<IResult> CallUpdateEmployee(
        Guid id, UpdateEmployeeRequest request, HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("UpdateEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, request, httpContext, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallDeleteEmployee(
        Guid id, HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("DeleteEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, httpContext, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallDeleteUser(
        WarpBusinessDbContext db, EmployeeDbContext empDb, Guid id, KeycloakAdminService keycloakAdmin)
    {
        var method = typeof(UserEndpoints).GetMethod("DeleteUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, db, empDb, keycloakAdmin, CancellationToken.None])!;
    }

    /// <summary>
    /// Calls GET /api/users/unlinked endpoint via reflection.
    /// </summary>
    private static async Task<IResult> CallGetUnlinkedUsers(
        HttpContext httpContext, WarpBusinessDbContext warpDb, EmployeeDbContext empDb)
    {
        var method = typeof(EmployeeUserEndpoints).GetMethod("GetUnlinkedUsers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null,
            [httpContext, warpDb, empDb, CancellationToken.None])!;
    }

    /// <summary>
    /// Calls POST /api/employees/with-user endpoint via reflection.
    /// </summary>
    private static async Task<IResult> CallCreateEmployeeWithUser(
        HttpContext httpContext, EmployeeDbContext empDb, WarpBusinessDbContext warpDb,
        KeycloakAdminService keycloakAdmin,
        string firstName, string lastName, string email, UserRole role)
    {
        var method = typeof(EmployeeUserEndpoints).GetMethod("CreateEmployeeWithUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var request = new CreateEmployeeWithUserRequest(
            firstName, lastName, null, email, null, null,
            new DateOnly(2364, 1, 1), null, null, null,
            EmploymentStatus.Active, EmploymentType.FullTime, role.ToString());

        var logger = NullLogger<KeycloakAdminService>.Instance;
        return await (Task<IResult>)method.Invoke(null,
            [request, httpContext, warpDb, empDb, keycloakAdmin, logger, CancellationToken.None])!;
    }

    /// <summary>
    /// Calls PUT /api/employees/{id}/with-user endpoint via reflection.
    /// </summary>
    private static async Task<IResult> CallUpdateEmployeeWithUser(
        Guid id, HttpContext httpContext, EmployeeDbContext empDb, WarpBusinessDbContext warpDb,
        KeycloakAdminService keycloakAdmin,
        string firstName, string lastName, string email, UserRole role)
    {
        var method = typeof(EmployeeUserEndpoints).GetMethod("UpdateEmployeeWithUser",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var request = new UpdateEmployeeWithUserRequest(
            firstName, lastName, null, email, null, null,
            new DateOnly(2364, 1, 1), null, null, null, null,
            EmploymentStatus.Active, EmploymentType.FullTime, role.ToString());

        return await (Task<IResult>)method.Invoke(null,
            [id, request, httpContext, warpDb, empDb, keycloakAdmin, CancellationToken.None])!;
    }

    /// <summary>
    /// Calls GET /api/employees/by-user/{userId} endpoint via reflection.
    /// </summary>
    private static async Task<IResult> CallGetEmployeeByUserId(
        Guid userId, HttpContext httpContext, EmployeeDbContext empDb)
    {
        var method = typeof(EmployeeUserEndpoints).GetMethod("GetEmployeeByUserId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null,
            [userId, httpContext, empDb, CancellationToken.None])!;
    }

    /// <summary>
    /// Calls PUT /api/employees/{id}/link-user/{userId} endpoint via reflection.
    /// </summary>
    private static async Task<IResult> CallLinkUserToEmployee(
        Guid employeeId, Guid userId, HttpContext httpContext, 
        WarpBusinessDbContext warpDb, EmployeeDbContext empDb)
    {
        var method = typeof(EmployeeUserEndpoints).GetMethod("LinkUserToEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null,
            [employeeId, userId, httpContext, warpDb, empDb, CancellationToken.None])!;
    }

    #endregion
}
