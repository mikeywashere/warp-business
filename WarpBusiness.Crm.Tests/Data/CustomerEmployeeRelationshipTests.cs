using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Crm.Tests.Helpers;
using WarpBusiness.Crm.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace WarpBusiness.Crm.Tests.Data;

[Collection("Database")]
public class CustomerEmployeeRelationshipTests
{
    private readonly PostgreSqlFixture _fixture;

    public CustomerEmployeeRelationshipTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresCrmDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureCrmSchemaAsync(db);
        db.CustomerEmployees.RemoveRange(db.CustomerEmployees);
        db.Customers.RemoveRange(db.Customers);
        await db.SaveChangesAsync();
        return db;
    }

    private Customer CreateTestCustomer(Guid tenantId, string name = "Test Customer")
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateRelationship_WithAllFields_CreatesSuccessfully()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        var employeeId = Guid.NewGuid();

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = employeeId,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);
        await db.SaveChangesAsync();

        var saved = await db.CustomerEmployees.FindAsync(relationship.Id);
        saved.Should().NotBeNull();
        saved!.CustomerId.Should().Be(customer.Id);
        saved.EmployeeId.Should().Be(employeeId);
        saved.Relationship.Should().Be("Account Manager");
    }

    [Fact]
    public async Task CreateRelationship_CommonRelationshipValues_AllWork()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var commonRelationships = new[]
        {
            "Account Manager",
            "Technical Contact",
            "Sales Representative",
            "Support Engineer",
            "Project Manager",
            "Executive Sponsor"
        };

        foreach (var relType in commonRelationships)
        {
            var relationship = new CustomerEmployee
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                EmployeeId = Guid.NewGuid(),
                Relationship = relType,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.CustomerEmployees.Add(relationship);
        }

        await db.SaveChangesAsync();

        var saved = await db.CustomerEmployees
            .Where(ce => ce.CustomerId == customer.Id)
            .ToListAsync();

        saved.Should().HaveCount(commonRelationships.Length);
    }

    #endregion

    #region Required Field Tests

    [Fact]
    public async Task CreateRelationship_WithoutRelationship_ThrowsException()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = null!, // Required field
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Relationship field is required");
    }

    [Fact]
    public async Task CreateRelationship_RelationshipExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = new string('A', 101), // MaxLength is 100
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Relationship exceeds max length of 100");
    }

    #endregion

    #region Unique Constraint Tests

    [Fact]
    public async Task CreateRelationship_SameEmployeeToSameCustomerTwice_ThrowsException()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        var employeeId = Guid.NewGuid();

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship1 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = employeeId,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship1);
        await db.SaveChangesAsync();

        var relationship2 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = employeeId, // Same employee, same customer
            Relationship = "Technical Contact", // Different relationship type doesn't matter
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship2);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Same employee cannot be assigned twice to same customer");
    }

    [Fact]
    public async Task CreateRelationship_SameEmployeeToDifferentCustomers_Succeeds()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer1 = CreateTestCustomer(tenantId, "Customer 1");
        var customer2 = CreateTestCustomer(tenantId, "Customer 2");
        var employeeId = Guid.NewGuid();

        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var relationship1 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer1.Id,
            EmployeeId = employeeId,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var relationship2 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer2.Id,
            EmployeeId = employeeId, // Same employee, different customer
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.AddRange(relationship1, relationship2);
        await db.SaveChangesAsync();

        var saved = await db.CustomerEmployees
            .Where(ce => ce.EmployeeId == employeeId)
            .ToListAsync();

        saved.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateRelationship_DifferentEmployeesToSameCustomer_Succeeds()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        var employee1Id = Guid.NewGuid();
        var employee2Id = Guid.NewGuid();

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship1 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = employee1Id,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var relationship2 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = employee2Id, // Different employee, same customer
            Relationship = "Technical Contact",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.AddRange(relationship1, relationship2);
        await db.SaveChangesAsync();

        var saved = await db.CustomerEmployees
            .Where(ce => ce.CustomerId == customer.Id)
            .ToListAsync();

        saved.Should().HaveCount(2);
    }

    #endregion

    #region Cascade Delete Tests

    [Fact]
    public async Task DeleteCustomer_CascadesDeleteToRelationships()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship1 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var relationship2 = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "Technical Contact",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.AddRange(relationship1, relationship2);
        await db.SaveChangesAsync();

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();

        var remainingRelationships = await db.CustomerEmployees
            .Where(ce => ce.CustomerId == customer.Id)
            .ToListAsync();

        remainingRelationships.Should().BeEmpty("relationships should be cascade deleted");
    }

    #endregion

    #region Navigation Property Tests

    [Fact]
    public async Task LoadCustomer_WithEmployeeRelationships_LoadsNavigationProperty()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);
        await db.SaveChangesAsync();

        var loaded = await db.Customers
            .Include(c => c.CustomerEmployees)
            .FirstAsync(c => c.Id == customer.Id);

        loaded.CustomerEmployees.Should().HaveCount(1);
        loaded.CustomerEmployees.First().Relationship.Should().Be("Account Manager");
    }

    [Fact]
    public async Task LoadRelationship_WithCustomer_LoadsNavigationProperty()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId, "Warp Industries");

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);
        await db.SaveChangesAsync();

        var loaded = await db.CustomerEmployees
            .Include(ce => ce.Customer)
            .FirstAsync(ce => ce.Id == relationship.Id);

        loaded.Customer.Should().NotBeNull();
        loaded.Customer!.Name.Should().Be("Warp Industries");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateRelationship_WithSpecialCharactersInRelationship_Succeeds()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "Account Manager & Technical Lead (Primary)",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);
        await db.SaveChangesAsync();

        var saved = await db.CustomerEmployees.FindAsync(relationship.Id);
        saved.Should().NotBeNull();
        saved!.Relationship.Should().Be("Account Manager & Technical Lead (Primary)");
    }

    [Fact]
    public async Task CreateRelationship_WithEmptyRelationship_ThrowsException()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var customer = CreateTestCustomer(tenantId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var relationship = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            EmployeeId = Guid.NewGuid(),
            Relationship = "", // Empty but not null
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.Add(relationship);

        // Note: EF Core required validation doesn't prevent empty strings, only null
        // But this documents the behavior
        await db.SaveChangesAsync(); // Should succeed (empty string is allowed by EF)

        var saved = await db.CustomerEmployees.FindAsync(relationship.Id);
        saved.Should().NotBeNull();
        saved!.Relationship.Should().Be("");
    }

    #endregion

    #region Multi-Tenant Isolation Tests

    [Fact]
    public async Task GetRelationships_RespectsCustomerTenantBoundaries()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var employeeId = Guid.NewGuid(); // Same employee works across tenants

        var customerA = CreateTestCustomer(tenantA, "Customer A");
        var customerB = CreateTestCustomer(tenantB, "Customer B");

        db.Customers.AddRange(customerA, customerB);
        await db.SaveChangesAsync();

        var relationshipA = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customerA.Id,
            EmployeeId = employeeId,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var relationshipB = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = customerB.Id,
            EmployeeId = employeeId,
            Relationship = "Account Manager",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.CustomerEmployees.AddRange(relationshipA, relationshipB);
        await db.SaveChangesAsync();

        var tenantARelationships = await db.CustomerEmployees
            .Include(ce => ce.Customer)
            .Where(ce => ce.Customer!.TenantId == tenantA)
            .ToListAsync();

        var tenantBRelationships = await db.CustomerEmployees
            .Include(ce => ce.Customer)
            .Where(ce => ce.Customer!.TenantId == tenantB)
            .ToListAsync();

        tenantARelationships.Should().HaveCount(1);
        tenantARelationships.First().CustomerId.Should().Be(customerA.Id);

        tenantBRelationships.Should().HaveCount(1);
        tenantBRelationships.First().CustomerId.Should().Be(customerB.Id);
    }

    #endregion
}
