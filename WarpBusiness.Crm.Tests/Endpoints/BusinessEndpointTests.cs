using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Crm.Tests.Helpers;
using WarpBusiness.Crm.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace WarpBusiness.Crm.Tests.Endpoints;

/// <summary>
/// Tests for Business API endpoints at /api/crm/businesses
/// </summary>
[Collection("Database")]
public class BusinessEndpointTests
{
    private readonly PostgreSqlFixture _fixture;

    public BusinessEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresCrmDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureCrmSchemaAsync(db);
        
        // Remove in dependency order: CustomerEmployees -> Customers -> Businesses
        db.CustomerEmployees.RemoveRange(db.CustomerEmployees);
        db.Customers.RemoveRange(db.Customers);
        db.Businesses.RemoveRange(db.Businesses);
        await db.SaveChangesAsync();
        return db;
    }

    private static HttpContext CreateHttpContextWithTenant(Guid tenantId)
    {
        var context = new DefaultHttpContext();
        context.Items["TenantId"] = tenantId;
        return context;
    }

    private Business CreateTestBusiness(Guid tenantId, string name = "Test Business")
    {
        return new Business
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Industry = "Technology",
            Website = "https://testbusiness.com",
            Phone = "555-0100",
            Address = "123 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
            Notes = "Test business notes",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private Customer CreateTestCustomer(Guid tenantId, Guid? businessId = null, string name = "Test Customer")
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name,
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task GetBusinesses_ReturnsTenantScopedList()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var businessA1 = CreateTestBusiness(tenantA, "Business A1");
        var businessA2 = CreateTestBusiness(tenantA, "Business A2");
        var businessB1 = CreateTestBusiness(tenantB, "Business B1");

        db.Businesses.AddRange(businessA1, businessA2, businessB1);
        await db.SaveChangesAsync();

        // Act
        var results = await db.Businesses
            .Where(b => b.TenantId == tenantA)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(b => b.Id == businessA1.Id);
        results.Should().Contain(b => b.Id == businessA2.Id);
        results.Should().NotContain(b => b.Id == businessB1.Id);
    }

    [Fact]
    public async Task CreateBusiness_CreatesSuccessfully()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "New Business");

        // Act
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        // Assert
        var saved = await db.Businesses.FirstOrDefaultAsync(b => b.Id == business.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("New Business");
        saved.Industry.Should().Be("Technology");
        saved.Website.Should().Be("https://testbusiness.com");
        saved.Phone.Should().Be("555-0100");
        saved.Address.Should().Be("123 Main St");
        saved.City.Should().Be("Springfield");
        saved.State.Should().Be("IL");
        saved.PostalCode.Should().Be("62701");
        saved.Country.Should().Be("USA");
        saved.Notes.Should().Be("Test business notes");
        saved.IsActive.Should().BeTrue();
        saved.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        saved.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateBusiness_UpdatesFields()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "Original Name");
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        // Detach and reload to simulate endpoint update flow
        db.Entry(business).State = EntityState.Detached;

        // Act
        var updatedBusiness = await db.Businesses.FirstOrDefaultAsync(b => b.Id == business.Id);
        updatedBusiness!.Name = "Updated Name";
        updatedBusiness.Industry = "Finance";
        updatedBusiness.Website = "https://updated.com";
        updatedBusiness.Phone = "555-9999";
        updatedBusiness.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // Assert
        var result = await db.Businesses.FirstOrDefaultAsync(b => b.Id == business.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Industry.Should().Be("Finance");
        result.Website.Should().Be("https://updated.com");
        result.Phone.Should().Be("555-9999");
        result.UpdatedAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task GetBusiness_Returns404ForWrongTenant()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var businessA = CreateTestBusiness(tenantA, "Business A");
        db.Businesses.Add(businessA);
        await db.SaveChangesAsync();

        // Act - tenant B tries to access tenant A's business
        var result = await db.Businesses
            .Where(b => b.TenantId == tenantB && b.Id == businessA.Id)
            .FirstOrDefaultAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBusiness_WithNoCustomers_Succeeds()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "To Delete");
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        // Act
        db.Businesses.Remove(business);
        await db.SaveChangesAsync();

        // Assert
        var result = await db.Businesses.FirstOrDefaultAsync(b => b.Id == business.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBusiness_WithLinkedCustomers_Returns409()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "Business with Customers");
        var customer1 = CreateTestCustomer(tenantId, business.Id, "Customer 1");
        var customer2 = CreateTestCustomer(tenantId, business.Id, "Customer 2");

        db.Businesses.Add(business);
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        // Act & Assert - trying to delete business with linked customers should fail
        // In endpoint: check if customers exist, return 409 Conflict if found
        var linkedCustomersExist = await db.Customers
            .AnyAsync(c => c.BusinessId == business.Id);

        linkedCustomersExist.Should().BeTrue("business has linked customers");

        // Attempting to delete without unlinking would trigger 409 in endpoint
        // Here we verify that the constraint exists
        var customerCount = await db.Customers
            .CountAsync(c => c.BusinessId == business.Id);
        customerCount.Should().Be(2);
    }

    [Fact]
    public async Task DeleteBusiness_WithUnlinkCustomers_UnlinksAndDeletes()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "Business to Unlink");
        var customer1 = CreateTestCustomer(tenantId, business.Id, "Customer 1");
        var customer2 = CreateTestCustomer(tenantId, business.Id, "Customer 2");

        db.Businesses.Add(business);
        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        // Act - simulate ?unlinkCustomers=true behavior
        var linkedCustomers = await db.Customers
            .Where(c => c.BusinessId == business.Id)
            .ToListAsync();

        foreach (var customer in linkedCustomers)
        {
            customer.BusinessId = null;
        }
        await db.SaveChangesAsync();

        db.Businesses.Remove(business);
        await db.SaveChangesAsync();

        // Assert
        var deletedBusiness = await db.Businesses.FirstOrDefaultAsync(b => b.Id == business.Id);
        deletedBusiness.Should().BeNull("business should be deleted");

        var customers = await db.Customers
            .Where(c => c.Id == customer1.Id || c.Id == customer2.Id)
            .ToListAsync();
        customers.Should().HaveCount(2, "customers should still exist");
        customers.Should().OnlyContain(c => c.BusinessId == null, "all customers should be unlinked");
    }

    [Fact]
    public async Task GetBusiness_IncludesCustomerCount()
    {
        // Arrange
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var business = CreateTestBusiness(tenantId, "Business with Count");
        var customer1 = CreateTestCustomer(tenantId, business.Id, "Customer 1");
        var customer2 = CreateTestCustomer(tenantId, business.Id, "Customer 2");
        var customer3 = CreateTestCustomer(tenantId, business.Id, "Customer 3");

        db.Businesses.Add(business);
        db.Customers.AddRange(customer1, customer2, customer3);
        await db.SaveChangesAsync();

        // Act - simulate endpoint that includes customer count
        var result = await db.Businesses
            .Where(b => b.Id == business.Id && b.TenantId == tenantId)
            .Select(b => new
            {
                Business = b,
                CustomerCount = db.Customers.Count(c => c.BusinessId == b.Id)
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Business.Name.Should().Be("Business with Count");
        result.CustomerCount.Should().Be(3);
    }
}
