using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Endpoints;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Tests.Endpoints;

[Collection("Database")]
public class EmployeeEndpointTests
{
    private readonly PostgreSqlFixture _fixture;

    public EmployeeEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<EmployeeDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresEmployeeDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureEmployeeSchemaAsync(db);
        db.Employees.RemoveRange(db.Employees);
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
        EmploymentStatus status = EmploymentStatus.Active,
        EmploymentType type = EmploymentType.FullTime,
        Guid? managerId = null) =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), "Command", "Captain", managerId, status, type, null);

    #region Serialization Tests (no DB)

    [Fact]
    public void CreateEmployee_WithEnumStrings_WorksProperly()
    {
        // This is the KEY test for the JsonStringEnumConverter bug fix.
        // Verify EmploymentStatus and EmploymentType serialize as strings, not integers.
        var request = MakeCreateRequest(
            status: EmploymentStatus.Active,
            type: EmploymentType.FullTime);

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"Active\"", "EmploymentStatus should serialize as string");
        json.Should().Contain("\"FullTime\"", "EmploymentType should serialize as string");
        json.Should().NotContain("\"EmploymentStatus\":0", "should not serialize as integer");
        json.Should().NotContain("\"EmploymentType\":0", "should not serialize as integer");

        // Round-trip: deserialize back and verify values preserved
        var deserialized = JsonSerializer.Deserialize<CreateEmployeeRequest>(json);
        deserialized.Should().NotBeNull();
        deserialized!.EmploymentStatus.Should().Be(EmploymentStatus.Active);
        deserialized.EmploymentType.Should().Be(EmploymentType.FullTime);
    }

    [Fact]
    public void AllEmploymentStatus_ValuesRoundTrip_AsStrings()
    {
        foreach (var status in Enum.GetValues<EmploymentStatus>())
        {
            var json = JsonSerializer.Serialize(status);
            json.Should().Be($"\"{status}\"", $"{status} should serialize as its name");

            var deserialized = JsonSerializer.Deserialize<EmploymentStatus>(json);
            deserialized.Should().Be(status);
        }
    }

    [Fact]
    public void AllEmploymentType_ValuesRoundTrip_AsStrings()
    {
        foreach (var type in Enum.GetValues<EmploymentType>())
        {
            var json = JsonSerializer.Serialize(type);
            json.Should().Be($"\"{type}\"", $"{type} should serialize as its name");

            var deserialized = JsonSerializer.Deserialize<EmploymentType>(json);
            deserialized.Should().Be(type);
        }
    }

    [Fact]
    public void EmployeeResponse_EnumsSerialize_AsStrings()
    {
        var response = new EmployeeResponse(
            Guid.NewGuid(), "EMP00001", "Data", "Soong", null,
            "data@enterprise.com", null, null, new DateOnly(2338, 2, 2), null,
            "Operations", "Second Officer", null,
            EmploymentStatus.OnLeave, EmploymentType.Contract,
            null, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow);

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("\"OnLeave\"");
        json.Should().Contain("\"Contract\"");
    }

    #endregion

    #region CRUD Tests (Testcontainers PostgreSQL)

    [Fact]
    public async Task CreateEmployee_WithValidData_ReturnsCreated()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest();

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.EmployeeNumber.Should().StartWith("EMP");
        created.Value.FirstName.Should().Be("Jean-Luc");
        created.Value.LastName.Should().Be("Picard");
        created.Value.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateEmployee_DuplicateEmail_ReturnsConflict()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest();

        await CallCreateEmployee(httpContext, request, db);

        // Try to create another employee with the same email in same tenant
        var httpContext2 = CreateHttpContextWithTenant(tenantId);
        var duplicate = MakeCreateRequest(firstName: "William", lastName: "Riker");
        var result = await CallCreateEmployee(httpContext2, duplicate, db);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateEmployee_InvalidManager_ReturnsBadRequest()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(managerId: Guid.NewGuid());

        var result = await CallCreateEmployee(httpContext, request, db);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAllEmployees_ReturnsTenantScopedResults()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Create employee in tenant A via endpoint
        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantA),
            MakeCreateRequest(email: "riker@tenantA.com"),
            db);

        // Insert tenant B employee directly with a different employee number
        // to avoid global unique constraint on EmployeeNumber
        db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = "EMP90001",
            FirstName = "Worf",
            LastName = "Son of Mogh",
            Email = "worf@tenantB.com",
            HireDate = new DateOnly(2364, 1, 1),
            EmploymentStatus = EmploymentStatus.Active,
            EmploymentType = EmploymentType.FullTime,
            TenantId = tenantB,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Query tenant A — should only see its own employees
        var result = await CallGetAllEmployees(CreateHttpContextWithTenant(tenantA), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        okResult.Value!.Count.Should().Be(1);
        okResult.Value.First().Email.Should().Be("riker@tenantA.com");
    }

    [Fact]
    public async Task GetEmployeeById_ReturnsCorrectEmployee()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            httpContext, MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        var result = await CallGetEmployeeById(
            employeeId, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.Id.Should().Be(employeeId);
        okResult.Value.FirstName.Should().Be("Jean-Luc");
    }

    [Fact]
    public async Task GetEmployeeById_WrongTenant_ReturnsNotFound()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantA), MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        // Try to access from tenant B
        var result = await CallGetEmployeeById(
            employeeId, CreateHttpContextWithTenant(tenantB), db);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateEmployee_ValidData_ReturnsOk()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        var updateRequest = new UpdateEmployeeRequest(
            "William", "Riker", "Thomas", "riker@enterprise.com", "555-1701-D",
            new DateOnly(2335, 8, 19), new DateOnly(2364, 1, 1), null,
            "Command", "First Officer", null,
            EmploymentStatus.Active, EmploymentType.FullTime, null);

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.FirstName.Should().Be("William");
        okResult.Value.LastName.Should().Be("Riker");
        okResult.Value.Email.Should().Be("riker@enterprise.com");
    }

    [Fact]
    public async Task UpdateEmployee_SelfAsManager_ReturnsBadRequest()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        var updateRequest = new UpdateEmployeeRequest(
            "Jean-Luc", "Picard", null, "picard@enterprise.com", null,
            null, new DateOnly(2364, 1, 1), null,
            "Command", "Captain", employeeId, // self as manager
            EmploymentStatus.Active, EmploymentType.FullTime, null);

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteEmployee_ReturnsNoContent()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        var result = await CallDeleteEmployee(
            employeeId, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<NoContent>();

        // Verify it's gone
        var getResult = await CallGetEmployeeById(
            employeeId, CreateHttpContextWithTenant(tenantId), db);
        getResult.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task DeleteEmployee_WrongTenant_ReturnsNotFound()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantA), MakeCreateRequest(), db);
        var employeeId = createResult.Value!.Id;

        // Try to delete from tenant B
        var result = await CallDeleteEmployee(
            employeeId, CreateHttpContextWithTenant(tenantB), db);

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
        HttpContext httpContext, CreateEmployeeRequest request, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("CreateEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var validator = new TestHelpers.PermissiveUserValidator();
        return await (Task<IResult>)method.Invoke(null, [request, httpContext, db, validator, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetAllEmployees(
        HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("GetAllEmployees",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [httpContext, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallGetEmployeeById(
        Guid id, HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("GetEmployeeById",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, httpContext, db, CancellationToken.None])!;
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

    #endregion
}
