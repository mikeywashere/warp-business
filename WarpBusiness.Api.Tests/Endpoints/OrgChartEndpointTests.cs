using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Endpoints;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Tests.Endpoints;

/// <summary>
/// Tests for:
///   GET  /api/employees/org-chart   — returns all tenant employees (with ManagerId for hierarchy)
///   Circular manager chain detection on UpdateEmployee (and CreateEmployee)
///
/// The org-chart endpoint (GetOrgChart) and circular-chain validation are being
/// added by Data in parallel. Tests that call GetOrgChart will fail early with
/// a clear message if the method has not yet been added to EmployeeEndpoints.
/// Circular-chain tests will fail at runtime (returning 200 instead of 400) until
/// Data adds the validation; they compile correctly today.
/// </summary>
[Collection("Database")]
public class OrgChartEndpointTests
{
    private readonly PostgreSqlFixture _fixture;

    public OrgChartEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    #region Setup Helpers

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
        Guid? managerId = null,
        decimal payAmount = 100000m,
        PayType payType = PayType.Salary,
        string currency = "USD") =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), "Command", "Captain", managerId, status, type, null, payAmount, payType, currency);

    private static UpdateEmployeeRequest MakeUpdateRequest(
        string firstName = "Jean-Luc",
        string lastName = "Picard",
        string email = "picard@enterprise.com",
        Guid? managerId = null,
        decimal payAmount = 100000m,
        PayType payType = PayType.Salary,
        string currency = "USD") =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), null, "Command", "Captain", managerId,
            EmploymentStatus.Active, EmploymentType.FullTime, null, payAmount, payType, currency);

    #endregion

    #region Org-Chart Endpoint Tests

    [Fact]
    public async Task GetOrgChart_ReturnsAllTenantEmployees()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);

        await CallCreateEmployee(httpContext, MakeCreateRequest(email: "a@enterprise.com", firstName: "Picard"), db);
        await CallCreateEmployee(httpContext, MakeCreateRequest(email: "b@enterprise.com", firstName: "Riker"), db);
        await CallCreateEmployee(httpContext, MakeCreateRequest(email: "c@enterprise.com", firstName: "Troi"), db);

        var result = await CallGetOrgChart(CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        okResult.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetOrgChart_DoesNotReturnOtherTenantEmployees()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantA),
            MakeCreateRequest(email: "picard@tenantA.com"),
            db);

        // Seed tenant B employee directly to avoid employee-number collision
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

        var result = await CallGetOrgChart(CreateHttpContextWithTenant(tenantA), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        okResult.Value!.Should().HaveCount(1);
        okResult.Value.Single().Email.Should().Be("picard@tenantA.com");
    }

    [Fact]
    public async Task GetOrgChart_ReturnsManagerIdWhenSet()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        // Create manager first
        var managerResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "manager@enterprise.com", firstName: "Data"),
            db);
        var managerId = managerResult.Value!.Id;

        // Create report with ManagerId pointing to the manager
        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "report@enterprise.com", firstName: "LaForge", managerId: managerId),
            db);

        var result = await CallGetOrgChart(CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        var report = okResult.Value!.Single(e => e.Email == "report@enterprise.com");
        report.ManagerId.Should().Be(managerId);
    }

    [Fact]
    public async Task GetOrgChart_Returns_ManagerId_Null_ForRoots()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "root1@enterprise.com", firstName: "Picard"),
            db);
        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "root2@enterprise.com", firstName: "Janeway"),
            db);

        var result = await CallGetOrgChart(CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        okResult.Value!.Should().HaveCount(2);
        okResult.Value!.Should().AllSatisfy(e => e.ManagerId.Should().BeNull("root employees have no manager"));
    }

    #endregion

    #region Circular Manager Chain Detection Tests

    [Fact]
    public async Task UpdateEmployee_RejectsCircularManagerChain()
    {
        // A → B → C, then try to set A's manager = C (would create A→B→C→A cycle)
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var aResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "a@enterprise.com", firstName: "Alpha"),
            db);
        var aId = aResult.Value!.Id;

        var bResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "b@enterprise.com", firstName: "Bravo", managerId: aId),
            db);
        var bId = bResult.Value!.Id;

        var cResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "c@enterprise.com", firstName: "Charlie", managerId: bId),
            db);
        var cId = cResult.Value!.Id;

        // Now try to set A's manager to C — would create a cycle: A→B→C→A
        var updateRequest = MakeUpdateRequest(
            firstName: "Alpha", email: "a@enterprise.com", managerId: cId);

        var result = await CallUpdateEmployee(aId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400, "assigning a manager that would create a circular chain must be rejected");

        var message = GetErrorMessage(result);
        message.Should().NotBeNullOrEmpty();
        message!.ToLower().Should().Contain("circular", "error should mention a circular dependency");
    }

    [Fact]
    public async Task UpdateEmployee_RejectsDirectCircle()
    {
        // A is manager of B; then try to make B the manager of A
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var aResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "kirk@enterprise.com", firstName: "James"),
            db);
        var aId = aResult.Value!.Id;

        var bResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "spock@enterprise.com", firstName: "Spock", managerId: aId),
            db);
        var bId = bResult.Value!.Id;

        // Try to make A report to B — direct two-node cycle
        var updateRequest = MakeUpdateRequest(
            firstName: "James", email: "kirk@enterprise.com", managerId: bId);

        var result = await CallUpdateEmployee(aId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(400, "a direct two-node manager cycle must be rejected");

        var message = GetErrorMessage(result);
        message.Should().NotBeNullOrEmpty();
        message!.ToLower().Should().Contain("circular", "error should mention a circular dependency");
    }

    #endregion

    #region Manager Validation Tests

    [Fact]
    public async Task CreateEmployee_WithValidManager_Succeeds()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var managerResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "manager@ds9.com", firstName: "Sisko"),
            db);
        var managerId = managerResult.Value!.Id;

        var result = await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "report@ds9.com", firstName: "Kira", managerId: managerId),
            db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.ManagerId.Should().Be(managerId);
    }

    [Fact]
    public async Task UpdateEmployee_CanClearManager()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var managerResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "chakotay@voyager.com", firstName: "Chakotay"),
            db);
        var managerId = managerResult.Value!.Id;

        var reportResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "kim@voyager.com", firstName: "Harry", managerId: managerId),
            db);
        var reportId = reportResult.Value!.Id;

        // Clear the manager by setting ManagerId to null
        var updateRequest = MakeUpdateRequest(
            firstName: "Harry", email: "kim@voyager.com", managerId: null);

        var result = await CallUpdateEmployee(reportId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.ManagerId.Should().BeNull("clearing the manager should set ManagerId to null");
    }

    #endregion

    #region Reflection Helpers

    private static int? GetStatusCode(IResult result)
    {
        var prop = result.GetType().GetProperty("StatusCode");
        return prop?.GetValue(result) as int?;
    }

    private static string? GetErrorMessage(IResult result)
    {
        // Try Value.message (anonymous type returned by Results.BadRequest(new { message = "..." }))
        var valueProp = result.GetType().GetProperty("Value");
        if (valueProp is null) return null;

        var value = valueProp.GetValue(result);
        if (value is null) return null;

        var messageProp = value.GetType().GetProperty("message");
        return messageProp?.GetValue(value) as string;
    }

    private static async Task<IResult> CallGetOrgChart(HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("GetOrgChart",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method is null)
            Assert.Fail("GetOrgChart method not found on EmployeeEndpoints. Has Data implemented the endpoint yet?");

        return await (Task<IResult>)method!.Invoke(null, [httpContext, db, CancellationToken.None])!;
    }

    private static async Task<IResult> CallCreateEmployee(
        HttpContext httpContext, CreateEmployeeRequest request, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("CreateEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var validator = new TestHelpers.PermissiveUserValidator();
        return await (Task<IResult>)method.Invoke(null, [request, httpContext, db, validator, CancellationToken.None])!;
    }

    private static async Task<IResult> CallUpdateEmployee(
        Guid id, UpdateEmployeeRequest request, HttpContext httpContext, EmployeeDbContext db)
    {
        var method = typeof(EmployeeEndpoints).GetMethod("UpdateEmployee",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<IResult>)method.Invoke(null, [id, request, httpContext, db, CancellationToken.None])!;
    }

    #endregion
}
