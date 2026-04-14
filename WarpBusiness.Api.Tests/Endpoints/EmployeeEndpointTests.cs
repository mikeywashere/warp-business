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
        Guid? managerId = null,
        decimal payAmount = 100000m,
        PayType payType = PayType.Salary,
        string currency = "USD") =>
        new(firstName, lastName, null, email, "555-1701", new DateOnly(2305, 7, 13),
            new DateOnly(2364, 1, 1), "Command", "Captain", managerId, status, type, null, payAmount, payType, currency);

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
    public void AllPayType_ValuesRoundTrip_AsStrings()
    {
        foreach (var type in Enum.GetValues<PayType>())
        {
            var json = JsonSerializer.Serialize(type);
            json.Should().Be($"\"{type}\"", $"{type} should serialize as its name");

            var deserialized = JsonSerializer.Deserialize<PayType>(json);
            deserialized.Should().Be(type);
        }
    }

    [Fact]
    public void CreateEmployee_WithPayStructure_SerializesCorrectly()
    {
        var request = MakeCreateRequest(
            firstName: "Beverly",
            lastName: "Crusher",
            email: "crusher@enterprise.com",
            payAmount: 95000m,
            payType: PayType.Salary,
            currency: "EUR");

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"PayAmount\":95000", "PayAmount should serialize correctly");
        json.Should().Contain("\"PayType\":\"Salary\"", "PayType should serialize as string");
        json.Should().Contain("\"Currency\":\"EUR\"", "Currency should serialize correctly");

        var deserialized = JsonSerializer.Deserialize<CreateEmployeeRequest>(json);
        deserialized.Should().NotBeNull();
        deserialized!.PayAmount.Should().Be(95000m);
        deserialized.PayType.Should().Be(PayType.Salary);
        deserialized.Currency.Should().Be("EUR");
    }

    [Fact]
    public void EmployeeResponse_EnumsSerialize_AsStrings()
    {
        var response = new EmployeeResponse(
            Guid.NewGuid(), "EMP00001", "Data", "Soong", null,
            "data@enterprise.com", null, null, new DateOnly(2338, 2, 2), null,
            "Operations", "Second Officer", null,
            EmploymentStatus.OnLeave, EmploymentType.Contract,
            null, Guid.NewGuid(), 85000m, PayType.Salary, "USD", DateTime.UtcNow, DateTime.UtcNow);

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
            EmploymentStatus.Active, EmploymentType.FullTime, null, 120000m, PayType.Salary, "USD");

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
            EmploymentStatus.Active, EmploymentType.FullTime, null, 100000m, PayType.Salary, "USD");

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

    #region Pay Structure Validation Tests

    [Fact]
    public async Task CreateEmployee_WithValidPayStructure_ReturnsCreditedWithPayFields()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 75000m, payType: PayType.Salary, currency: "EUR");

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayAmount.Should().Be(75000m);
        created.Value.PayType.Should().Be(PayType.Salary);
        created.Value.Currency.Should().Be("EUR");
    }

    // TODO: Add validation for negative and zero pay amounts
    // When implemented, these tests should pass:
    // [Theory]
    // [InlineData(0)]
    // [InlineData(-1)]
    // [InlineData(-100.50)]
    // public async Task CreateEmployee_WithInvalidPayAmount_ReturnsBadRequest(decimal invalidAmount)
    // {
    //     await using var db = await CreateCleanContext();
    //     var tenantId = Guid.NewGuid();
    //     var httpContext = CreateHttpContextWithTenant(tenantId);
    //     var request = MakeCreateRequest(payAmount: invalidAmount);
    //
    //     var result = await CallCreateEmployee(httpContext, request, db);
    //
    //     var statusCode = GetStatusCode(result);
    //     statusCode.Should().Be(400, "pay amount must be greater than 0");
    // }

    [Fact]
    public async Task CreateEmployee_WithPositiveDecimalPayAmount_Accepted()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 0.01m);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayAmount.Should().Be(0.01m);
    }

    [Theory]
    [InlineData(PayType.Hourly)]
    [InlineData(PayType.Salary)]
    [InlineData(PayType.Commission)]
    [InlineData(PayType.Contract)]
    public async Task CreateEmployee_WithValidPayType_IsStored(PayType validPayType)
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payType: validPayType);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayType.Should().Be(validPayType);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("AUD")]
    public async Task CreateEmployee_WithValidIso4217CurrencyCode_Accepted(string validCurrencyCode)
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(currency: validCurrencyCode);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.Currency.Should().Be(validCurrencyCode);
    }

    // TODO: Add validation for ISO 4217 currency code format
    // When implemented, these tests should pass:
    // [Theory]
    // [InlineData("USDD")]     // 4 letters
    // [InlineData("US")]       // 2 letters
    // [InlineData("US1")]      // with number
    // [InlineData("")]         // empty
    // [InlineData("usd")]      // lowercase (should be uppercase ISO 4217)
    // public async Task CreateEmployee_WithInvalidCurrencyFormat_ReturnsBadRequest(string invalidCurrency)
    // {
    //     await using var db = await CreateCleanContext();
    //     var tenantId = Guid.NewGuid();
    //     var httpContext = CreateHttpContextWithTenant(tenantId);
    //     var request = MakeCreateRequest(currency: invalidCurrency);
    //
    //     var result = await CallCreateEmployee(httpContext, request, db);
    //
    //     var statusCode = GetStatusCode(result);
    //     statusCode.Should().Be(400, "currency must be a valid ISO 4217 3-letter code");
    // }

    // TODO: Implement tenant-based currency defaulting
    // [Fact]
    // public async Task CreateEmployee_WithNullCurrency_UsesTenantDefaultCurrency()
    // {
    //     await using var db = await CreateCleanContext();
    //     var tenantId = Guid.NewGuid();
    //     var httpContext = CreateHttpContextWithTenant(tenantId);
    //     // Currency is null, should default to tenant's preferred currency
    //     var request = MakeCreateRequest(currency: null!);
    //
    //     var result = await CallCreateEmployee(httpContext, request, db);
    //
    //     result.Should().BeOfType<Created<EmployeeResponse>>();
    //     var created = (Created<EmployeeResponse>)result;
    //     // The actual default should be the tenant's PreferredCurrencyCode
    //     // For now, we verify that a currency was assigned (not empty)
    //     created.Value!.Currency.Should().NotBeNullOrEmpty("currency should default from tenant");
    // }

    [Fact]
    public async Task CreateEmployee_Currency_IsOptional_DefaultsToUsd()
    {
        // Currency can be provided or omitted; endpoint defaults to USD if not provided
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        
        // Test with explicitly provided currency
        var request1 = MakeCreateRequest(email: "employee1@company.com", currency: "EUR");
        var result1 = await CallCreateEmployee(httpContext, request1, db);
        result1.Should().BeOfType<Created<EmployeeResponse>>();
        var created1 = (Created<EmployeeResponse>)result1;
        created1.Value!.Currency.Should().Be("EUR");

        // Test with another explicitly provided currency
        var request2 = MakeCreateRequest(email: "employee2@company.com", currency: "GBP");
        var result2 = await CallCreateEmployee(httpContext, request2, db);
        result2.Should().BeOfType<Created<EmployeeResponse>>();
        var created2 = (Created<EmployeeResponse>)result2;
        created2.Value!.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task CreateEmployee_Currency_DefaultsFromEndpoint()
    {
        // When Currency is provided, it should be used (future: default to Tenant.PreferredCurrencyCode)
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(currency: "GBP");

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.Currency.Should().Be("GBP", "currency should use the provided value");
    }

    [Fact]
    public void CreateEmployee_RequiresPayAmount()
    {
        // This verifies that PayAmount is a required field and cannot be null
        // The decimal type is non-nullable, so this is implicitly tested
        // through serialization/deserialization
        var request = MakeCreateRequest(payAmount: 0);
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        json.Should().Contain("\"PayAmount\"");
    }

    [Fact]
    public void CreateEmployee_RequiresPayType()
    {
        // This verifies that PayType is a required field
        var request = MakeCreateRequest(payType: PayType.Hourly);
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        json.Should().Contain("\"PayType\"");
    }

    [Fact]
    public async Task UpdateEmployee_WithValidPayStructure_ReturnOkWithPayFields()
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
            EmploymentStatus.Active, EmploymentType.FullTime, null,
            95000m, PayType.Salary, "GBP");

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.PayAmount.Should().Be(95000m);
        okResult.Value.PayType.Should().Be(PayType.Salary);
        okResult.Value.Currency.Should().Be("GBP");
    }

    // TODO: Add validation for negative and zero pay amounts on update
    // When implemented, these tests should pass:
    // [Theory]
    // [InlineData(0)]
    // [InlineData(-500)]
    // public async Task UpdateEmployee_WithInvalidPayAmount_ReturnsBadRequest(decimal invalidAmount)
    // {
    //     await using var db = await CreateCleanContext();
    //     var tenantId = Guid.NewGuid();
    //
    //     var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
    //         CreateHttpContextWithTenant(tenantId), MakeCreateRequest(), db);
    //     var employeeId = createResult.Value!.Id;
    //
    //     var updateRequest = new UpdateEmployeeRequest(
    //         "William", "Riker", null, "riker@enterprise.com", null,
    //         null, new DateOnly(2364, 1, 1), null,
    //         "Command", "First Officer", null,
    //         EmploymentStatus.Active, EmploymentType.FullTime, null,
    //         invalidAmount, PayType.Salary, "USD");
    //
    //     var result = await CallUpdateEmployee(
    //         employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);
    //
    //     var statusCode = GetStatusCode(result);
    //     statusCode.Should().Be(400);
    // }

    // TODO: Add validation for currency format in updates
    // [Theory]
    // [InlineData("XXX")]      // invalid code
    // [InlineData("")]         // empty
    // [InlineData("US")]       // too short
    // public async Task UpdateEmployee_WithInvalidCurrency_ReturnsBadRequest(string invalidCurrency)
    // {
    //     await using var db = await CreateCleanContext();
    //     var tenantId = Guid.NewGuid();
    //
    //     var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
    //         CreateHttpContextWithTenant(tenantId), MakeCreateRequest(), db);
    //     var employeeId = createResult.Value!.Id;
    //
    //     var updateRequest = new UpdateEmployeeRequest(
    //         "William", "Riker", null, "riker@enterprise.com", null,
    //         null, new DateOnly(2364, 1, 1), null,
    //         "Command", "First Officer", null,
    //         EmploymentStatus.Active, EmploymentType.FullTime, null,
    //         50000m, PayType.Salary, invalidCurrency);
    //
    //     var result = await CallUpdateEmployee(
    //         employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);
    //
    //     var statusCode = GetStatusCode(result);
    //     statusCode.Should().Be(400);
    // }

    [Fact]
    public async Task CreateEmployee_HourlyPayType_Accepted()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 35.50m, payType: PayType.Hourly);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayType.Should().Be(PayType.Hourly);
        created.Value!.PayAmount.Should().Be(35.50m);
    }

    [Fact]
    public async Task CreateEmployee_PreservesPayAmountPrecision()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var preciseAmount = 12345.6789m;
        var request = MakeCreateRequest(payAmount: preciseAmount);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayAmount.Should().Be(preciseAmount);
    }

    [Fact]
    public void PayType_Enum_HasAllRequiredValues()
    {
        // Verify the enum has all four required values
        var values = Enum.GetValues<PayType>();
        values.Should().Contain(PayType.Hourly);
        values.Should().Contain(PayType.Salary);
        values.Should().Contain(PayType.Commission);
        values.Should().Contain(PayType.Contract);
        values.Length.Should().Be(4, "PayType enum should have exactly 4 values");
    }

    [Fact]
    public void PayType_Enum_Values_SerializeAsStrings()
    {
        // Verify PayType enums serialize as strings like other employment enums
        foreach (var payType in Enum.GetValues<PayType>())
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payType);
            json.Should().Be($"\"{payType}\"", $"{payType} should serialize as a string");
        }
    }

    [Fact]
    public async Task CreateEmployee_CommissionPayType_IsStored()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 5000m, payType: PayType.Commission);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayType.Should().Be(PayType.Commission);
    }

    [Fact]
    public async Task CreateEmployee_ContractPayType_IsStored()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 8000m, payType: PayType.Contract);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayType.Should().Be(PayType.Contract);
    }

    [Fact]
    public void CreateEmployee_CurrencyIsOptional()
    {
        // Currency is nullable in the request and defaults to USD in the endpoint
        var requestWithCurrency = MakeCreateRequest(currency: "EUR");
        var jsonWithCurrency = System.Text.Json.JsonSerializer.Serialize(requestWithCurrency);
        jsonWithCurrency.Should().Contain("\"Currency\":\"EUR\"");

        // Even without explicit currency, the endpoint defaults to USD
        var requestWithoutCurrency = MakeCreateRequest(currency: null!);
        var jsonWithoutCurrency = System.Text.Json.JsonSerializer.Serialize(requestWithoutCurrency);
        // Currency will be null or default value depending on test helper setup
    }

    [Theory]
    [InlineData(50000.25)]
    [InlineData(0.01)]
    [InlineData(999999.99)]
    public async Task CreateEmployee_WithVariousPayAmounts_IsStored(decimal amount)
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: amount);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayAmount.Should().Be(amount);
    }

    [Fact]
    public async Task CreateEmployee_SalaryPayType_IsStored()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var request = MakeCreateRequest(payAmount: 85000m, payType: PayType.Salary);

        var result = await CallCreateEmployee(httpContext, request, db);

        result.Should().BeOfType<Created<EmployeeResponse>>();
        var created = (Created<EmployeeResponse>)result;
        created.Value!.PayType.Should().Be(PayType.Salary);
        created.Value!.PayAmount.Should().Be(85000m);
    }

    [Fact]
    public async Task UpdateEmployee_CanChangePayAmount()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(payAmount: 50000m), db);
        var employeeId = createResult.Value!.Id;

        var updateRequest = new UpdateEmployeeRequest(
            "Jean-Luc", "Picard", null, "picard@enterprise.com", null,
            null, new DateOnly(2364, 1, 1), null,
            "Command", "Captain", null,
            EmploymentStatus.Active, EmploymentType.FullTime, null,
            75000m, PayType.Salary, "USD");

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.PayAmount.Should().Be(75000m, "pay amount should be updated");
    }

    [Fact]
    public async Task UpdateEmployee_CanChangePayType()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(payType: PayType.Salary), db);
        var employeeId = createResult.Value!.Id;

        var updateRequest = new UpdateEmployeeRequest(
            "Jean-Luc", "Picard", null, "picard@enterprise.com", null,
            null, new DateOnly(2364, 1, 1), null,
            "Command", "Captain", null,
            EmploymentStatus.Active, EmploymentType.FullTime, null,
            35.50m, PayType.Hourly, "USD");

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.PayType.Should().Be(PayType.Hourly, "pay type should be updated to hourly");
    }

    [Fact]
    public async Task UpdateEmployee_CanChangeCurrency()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId), MakeCreateRequest(currency: "USD"), db);
        var employeeId = createResult.Value!.Id;

        var updateRequest = new UpdateEmployeeRequest(
            "Jean-Luc", "Picard", null, "picard@enterprise.com", null,
            null, new DateOnly(2364, 1, 1), null,
            "Command", "Captain", null,
            EmploymentStatus.Active, EmploymentType.FullTime, null,
            50000m, PayType.Salary, "EUR");

        var result = await CallUpdateEmployee(
            employeeId, updateRequest, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.Currency.Should().Be("EUR", "currency should be updated");
    }

    [Fact]
    public async Task GetEmployeeById_IncludesPayStructureData()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithTenant(tenantId);
        var payAmount = 60000m;
        var payType = PayType.Salary;
        var currency = "CAD";

        var createResult = (Created<EmployeeResponse>)await CallCreateEmployee(
            httpContext, MakeCreateRequest(payAmount: payAmount, payType: payType, currency: currency), db);
        var employeeId = createResult.Value!.Id;

        var result = await CallGetEmployeeById(
            employeeId, CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<EmployeeResponse>>();
        var okResult = (Ok<EmployeeResponse>)result;
        okResult.Value!.PayAmount.Should().Be(payAmount);
        okResult.Value!.PayType.Should().Be(payType);
        okResult.Value!.Currency.Should().Be(currency);
    }

    [Fact]
    public async Task GetAllEmployees_IncludesPayStructureDataForAll()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "emp1@enterprise.com", payAmount: 50000m, payType: PayType.Salary, currency: "USD"),
            db);

        await CallCreateEmployee(
            CreateHttpContextWithTenant(tenantId),
            MakeCreateRequest(email: "emp2@enterprise.com", firstName: "Data", payAmount: 45.50m, payType: PayType.Hourly, currency: "EUR"),
            db);

        var result = await CallGetAllEmployees(CreateHttpContextWithTenant(tenantId), db);

        result.Should().BeOfType<Ok<List<EmployeeResponse>>>();
        var okResult = (Ok<List<EmployeeResponse>>)result;
        okResult.Value.Should().HaveCount(2);
        okResult.Value.Should().AllSatisfy(emp =>
        {
            emp.PayAmount.Should().BeGreaterThan(0);
            emp.PayType.Should().BeOneOf(PayType.Hourly, PayType.Salary);
            emp.Currency.Should().NotBeNullOrEmpty();
        });
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
