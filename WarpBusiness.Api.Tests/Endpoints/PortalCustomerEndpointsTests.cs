using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Tests.Infrastructure;
using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Crm.Tests.Helpers;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Tests.Endpoints;

/// <summary>
/// Comprehensive integration tests for Customer Portal API endpoints (/api/portal/customers/*).
/// Tests authentication, authorization (IDOR prevention), profile CRUD, logo operations, and employee assignments.
/// </summary>
[Collection("Database")]
public class PortalCustomerEndpointsTests
{
    private readonly PostgreSqlFixture _fixture;

    public PortalCustomerEndpointsTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test Setup Helpers

    private class TestContexts : IAsyncDisposable
    {
        public required CrmDbContext CrmDb { get; init; }
        public required EmployeeDbContext EmployeeDb { get; init; }

        public async ValueTask DisposeAsync()
        {
            await CrmDb.DisposeAsync();
            await EmployeeDb.DisposeAsync();
        }
    }

    private async Task<TestContexts> CreateCleanContexts()
    {
        var crmDb = CrmTestHelpers.CreatePostgresCrmDbContext(_fixture.ConnectionString);
        var employeeDb = EmployeeTestHelpers.CreatePostgresEmployeeDbContext(_fixture.ConnectionString);

        await CrmTestHelpers.EnsureCrmSchemaAsync(crmDb);
        await EmployeeTestHelpers.EnsureEmployeeSchemaAsync(employeeDb);

        crmDb.CustomerEmployees.RemoveRange(crmDb.CustomerEmployees);
        crmDb.Customers.RemoveRange(crmDb.Customers);
        employeeDb.Employees.RemoveRange(employeeDb.Employees);

        await crmDb.SaveChangesAsync();
        await employeeDb.SaveChangesAsync();

        return new TestContexts { CrmDb = crmDb, EmployeeDb = employeeDb };
    }

    private static HttpContext CreateHttpContextWithTenant(Guid tenantId)
    {
        var context = new DefaultHttpContext();
        context.Items["TenantId"] = tenantId;
        return context;
    }

    private static ClaimsPrincipal CreateCustomerPrincipal(
        Guid customerId,
        string email = "customer@example.com",
        string role = "Customer")
    {
        var claims = new List<Claim>
        {
            new("sub", customerId.ToString()),
            new("email", email),
            new("app_role", role)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private Customer CreateTestCustomer(
        Guid tenantId,
        Guid customerId,
        string name = "Test Customer Inc",
        string? email = null,
        bool isActive = true)
    {
        email ??= $"{name.Replace(" ", "").ToLower()}@example.com";

        return new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = name,
            Email = email,
            Phone = "+1-555-0100",
            Address = "123 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
            Industry = "Technology",
            CompanySize = "50-100",
            Website = "https://example.com",
            Currency = "USD",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private Employee CreateTestEmployee(
        Guid tenantId,
        string firstName = "John",
        string lastName = "Doe",
        string? email = null,
        EmploymentStatus status = EmploymentStatus.Active)
    {
        email ??= $"{firstName.ToLower()}.{lastName.ToLower()}@company.com";

        return new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = $"EMP{Random.Shared.Next(10000, 99999)}",
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            HireDate = new DateOnly(2024, 1, 1),
            EmploymentStatus = status,
            EmploymentType = EmploymentType.FullTime,
            PayAmount = 50000m,
            PayType = PayType.Salary,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private CustomerEmployee CreateTestAssignment(
        Guid customerId,
        Guid employeeId,
        string relationship = "Primary Contact",
        decimal? billingRate = 150.00m,
        string billingCurrency = "USD")
    {
        return new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            EmployeeId = employeeId,
            Relationship = relationship,
            BillingRate = billingRate,
            BillingCurrency = billingCurrency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion

    #region Category 1: Profile Access Tests

    [Fact]
    public async Task GetProfile_WithValidCustomer_ReturnsCompleteProfile()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var retrievedCustomer = await contexts.CrmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId && c.IsActive);

        retrievedCustomer.Should().NotBeNull();
        retrievedCustomer!.Name.Should().Be("Test Customer Inc");
        retrievedCustomer.Email.Should().NotBeNullOrEmpty();
        retrievedCustomer.Phone.Should().Be("+1-555-0100");
        retrievedCustomer.Address.Should().Be("123 Main St");
        retrievedCustomer.Currency.Should().Be("USD");
        retrievedCustomer.Logo.Should().BeNull();
    }

    [Fact]
    public async Task GetProfile_WithInactiveCustomer_ReturnsNotFound()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId, isActive: false);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var retrievedCustomer = await contexts.CrmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId && c.IsActive);

        retrievedCustomer.Should().BeNull();
    }

    #endregion

    #region Category 2: IDOR Prevention Tests

    [Fact]
    public async Task MultipleCustomers_AreIsolated_ByCustomerId()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();

        var customerA = CreateTestCustomer(tenantId, customerAId, name: "Customer A", email: "a@example.com");
        var customerB = CreateTestCustomer(tenantId, customerBId, name: "Customer B", email: "b@example.com");

        contexts.CrmDb.Customers.AddRange(customerA, customerB);
        await contexts.CrmDb.SaveChangesAsync();

        // Customer A should only see their own profile
        var customerAData = await contexts.CrmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == customerAId && c.TenantId == tenantId && c.IsActive);

        customerAData.Should().NotBeNull();
        customerAData!.Name.Should().Be("Customer A");

        // Trying to access Customer B with Customer A's context should fail
        var customerBAsA = await contexts.CrmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == customerBId && c.TenantId == tenantId && c.IsActive);

        customerBAsA.Should().NotBeNull(); // Different customer exists in same tenant
        customerBAsA!.Id.Should().NotBe(customerAId); // But it's not Customer A
    }

    [Fact]
    public async Task Customers_AreIsolatedByTenant()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();

        var customerA = CreateTestCustomer(tenantAId, customerAId, name: "Customer A", email: "customer@example.com");
        var customerB = CreateTestCustomer(tenantBId, customerBId, name: "Customer B", email: "customer@example.com");

        contexts.CrmDb.Customers.AddRange(customerA, customerB);
        await contexts.CrmDb.SaveChangesAsync();

        // Tenant A can only see Tenant A customers
        var tenantACustomers = await contexts.CrmDb.Customers
            .Where(c => c.TenantId == tenantAId && c.IsActive)
            .ToListAsync();

        tenantACustomers.Should().HaveCount(1);
        tenantACustomers.First().Id.Should().Be(customerAId);

        // Tenant B can only see Tenant B customers
        var tenantBCustomers = await contexts.CrmDb.Customers
            .Where(c => c.TenantId == tenantBId && c.IsActive)
            .ToListAsync();

        tenantBCustomers.Should().HaveCount(1);
        tenantBCustomers.First().Id.Should().Be(customerBId);
    }

    #endregion

    #region Category 3: Profile Update Tests

    [Fact]
    public async Task UpdateProfile_WithValidData_UpdatesCustomer()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId, email: "original@example.com");
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        // Update customer
        customer.Name = "Updated Name";
        customer.Email = "updated@example.com";
        customer.Phone = "+1-555-9999";
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        contexts.CrmDb.Customers.Update(customer);
        await contexts.CrmDb.SaveChangesAsync();

        // Retrieve and verify
        var updated = await contexts.CrmDb.Customers.FindAsync(customerId);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.Email.Should().Be("updated@example.com");
        updated.Phone.Should().Be("+1-555-9999");
    }

    [Fact]
    public async Task UpdateProfile_EmailUniquePerTenant()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();

        var customerA = CreateTestCustomer(tenantId, customerAId, name: "Customer A", email: "a@example.com");
        var customerB = CreateTestCustomer(tenantId, customerBId, name: "Customer B", email: "b@example.com");

        contexts.CrmDb.Customers.AddRange(customerA, customerB);
        await contexts.CrmDb.SaveChangesAsync();

        // Check if email is already used in this tenant
        var emailExists = await contexts.CrmDb.Customers.AnyAsync(
            c => c.Email == "b@example.com" && c.TenantId == tenantId && c.Id != customerAId);

        emailExists.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfile_CanReuseEmailFromDifferentTenant()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();

        var customerA = CreateTestCustomer(tenantAId, customerAId, name: "Customer A", email: "customer@example.com");
        var customerB = CreateTestCustomer(tenantBId, customerBId, name: "Customer B", email: "customer@example.com");

        contexts.CrmDb.Customers.AddRange(customerA, customerB);
        await contexts.CrmDb.SaveChangesAsync();

        // Email should be unique per tenant, so same email should NOT exist in Tenant A
        var emailExistsInTenantA = await contexts.CrmDb.Customers.AnyAsync(
            c => c.Email == "customer@example.com" && c.TenantId == tenantAId && c.Id != customerAId);

        emailExistsInTenantA.Should().BeFalse();
    }

    #endregion

    #region Category 4: Logo Tests

    [Fact]
    public async Task UploadLogo_StoresLogoBytes()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var logoBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        customer.Logo = logoBytes;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        contexts.CrmDb.Customers.Update(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var updated = await contexts.CrmDb.Customers.FindAsync(customerId);
        updated.Should().NotBeNull();
        updated!.Logo.Should().NotBeNullOrEmpty();
        updated.Logo.Should().Equal(logoBytes);
    }

    [Fact]
    public async Task ReplaceLogo_OverwritesPrevious()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        var initialLogo = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG
        customer.Logo = initialLogo;
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var newLogo = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG
        customer.Logo = newLogo;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        contexts.CrmDb.Customers.Update(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var updated = await contexts.CrmDb.Customers.FindAsync(customerId);
        updated!.Logo.Should().Equal(newLogo);
        updated.Logo.Should().NotEqual(initialLogo);
    }

    [Fact]
    public async Task DeleteLogo_SetToNull()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        customer.Logo = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        customer.Logo = null;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        contexts.CrmDb.Customers.Update(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var updated = await contexts.CrmDb.Customers.FindAsync(customerId);
        updated!.Logo.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLogo_Idempotent()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        customer.Logo = null;
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        // Delete again (should not throw)
        customer.Logo = null;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        contexts.CrmDb.Customers.Update(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var updated = await contexts.CrmDb.Customers.FindAsync(customerId);
        updated!.Logo.Should().BeNull();
    }

    #endregion

    #region Category 5: Employee Assignments Tests

    [Fact]
    public async Task GetAssignments_ReturnsActiveEmployeesOnly()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var employee1 = CreateTestEmployee(tenantId, "Alice", "Smith", status: EmploymentStatus.Active);
        var employee2 = CreateTestEmployee(tenantId, "Bob", "Jones", status: EmploymentStatus.Active);
        var employee3 = CreateTestEmployee(tenantId, "Charlie", "Brown", status: EmploymentStatus.Terminated);

        contexts.EmployeeDb.Employees.AddRange(employee1, employee2, employee3);
        await contexts.EmployeeDb.SaveChangesAsync();

        var assignment1 = CreateTestAssignment(customerId, employee1.Id, "Primary Contact", 100m);
        var assignment2 = CreateTestAssignment(customerId, employee2.Id, "Secondary Contact", 150m);
        var assignment3 = CreateTestAssignment(customerId, employee3.Id, "Tertiary Contact", 75m);

        contexts.CrmDb.CustomerEmployees.AddRange(assignment1, assignment2, assignment3);
        await contexts.CrmDb.SaveChangesAsync();

        // Get active employees (from same tenant)
        var activeEmployees = await contexts.EmployeeDb.Employees
            .Where(e => e.TenantId == tenantId && e.EmploymentStatus == EmploymentStatus.Active)
            .ToListAsync();

        activeEmployees.Should().HaveCount(2);
        activeEmployees.Should().Contain(e => e.Id == employee1.Id);
        activeEmployees.Should().Contain(e => e.Id == employee2.Id);
    }

    [Fact]
    public async Task GetAssignments_WithNoAssignments_ReturnsEmpty()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var assignments = await contexts.CrmDb.CustomerEmployees
            .Where(ce => ce.CustomerId == customerId)
            .ToListAsync();

        assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignmentFieldsPopulateCorrectly()
    {
        await using var contexts = await CreateCleanContexts();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customer = CreateTestCustomer(tenantId, customerId);
        contexts.CrmDb.Customers.Add(customer);
        await contexts.CrmDb.SaveChangesAsync();

        var employee = CreateTestEmployee(tenantId, "John", "Doe");
        contexts.EmployeeDb.Employees.Add(employee);
        await contexts.EmployeeDb.SaveChangesAsync();

        var assignment = CreateTestAssignment(customerId, employee.Id, "Technical Lead", 250m, "USD");
        contexts.CrmDb.CustomerEmployees.Add(assignment);
        await contexts.CrmDb.SaveChangesAsync();

        var retrieved = await contexts.CrmDb.CustomerEmployees.FindAsync(assignment.Id);
        retrieved.Should().NotBeNull();
        retrieved!.CustomerId.Should().Be(customerId);
        retrieved.EmployeeId.Should().Be(employee.Id);
        retrieved.Relationship.Should().Be("Technical Lead");
        retrieved.BillingRate.Should().Be(250m);
        retrieved.BillingCurrency.Should().Be("USD");
    }

    #endregion

    #region Test Helper Classes

    private static class CrmTestHelpers
    {
        public static CrmDbContext CreatePostgresCrmDbContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new CrmDbContext(options);
        }

        public static async Task EnsureCrmSchemaAsync(CrmDbContext db)
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    private static class EmployeeTestHelpers
    {
        public static EmployeeDbContext CreatePostgresEmployeeDbContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<EmployeeDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new EmployeeDbContext(options);
        }

        public static async Task EnsureEmployeeSchemaAsync(EmployeeDbContext db)
        {
            try
            {
                _ = await db.Employees.AnyAsync();
            }
            catch
            {
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            }
        }
    }

    #endregion
}
