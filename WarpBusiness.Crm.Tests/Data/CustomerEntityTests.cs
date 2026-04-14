using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Crm.Tests.Helpers;
using WarpBusiness.Crm.Tests.Infrastructure;

namespace WarpBusiness.Crm.Tests.Data;

[Collection("Database")]
public class CustomerEntityTests
{
    private readonly PostgreSqlFixture _fixture;

    public CustomerEntityTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CrmDbContext> CreateCleanContext()
    {
        var db = TestHelpers.CreatePostgresCrmDbContext(_fixture.ConnectionString);
        await TestHelpers.EnsureCrmSchemaAsync(db);
        db.Customers.RemoveRange(db.Customers);
        db.CustomerEmployees.RemoveRange(db.CustomerEmployees);
        await db.SaveChangesAsync();
        return db;
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateCustomer_WithAllFields_CreatesSuccessfully()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Warp Industries",
            Email = "contact@warpindustries.com",
            Phone = "555-1234",
            Address = "123 Enterprise Way",
            City = "San Francisco",
            State = "CA",
            PostalCode = "94102",
            Country = "USA",
            Industry = "Technology",
            CompanySize = "51-200",
            Website = "https://warpindustries.com",
            Notes = "Premier customer",
            Currency = "EUR",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var saved = await db.Customers.FindAsync(customer.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Warp Industries");
        saved.Email.Should().Be("contact@warpindustries.com");
        saved.TenantId.Should().Be(tenantId);
        saved.Currency.Should().Be("EUR");
        saved.IsActive.Should().BeTrue("IsActive defaults to true");
    }

    [Fact]
    public async Task CreateCustomer_WithMinimalFields_CreatesSuccessfully()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Minimal Corp",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var saved = await db.Customers.FindAsync(customer.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Minimal Corp");
        saved.Email.Should().BeNull();
        saved.Phone.Should().BeNull();
        saved.Currency.Should().Be("USD");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Customer_DefaultValues_AreCorrect()
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Customer"
        };

        customer.IsActive.Should().BeTrue("IsActive should default to true");
        customer.CustomerEmployees.Should().NotBeNull().And.BeEmpty("CustomerEmployees should default to empty collection");
    }

    #endregion

    #region Required Field Tests

    [Fact]
    public async Task CreateCustomer_WithoutName_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = null!, // Required field
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Name is required");
    }

    #endregion

    #region Field Validation Tests

    [Fact]
    public async Task CreateCustomer_NameExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = new string('A', 501), // MaxLength is 500
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Name exceeds max length of 500");
    }

    [Fact]
    public async Task CreateCustomer_EmailExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test",
            Email = new string('a', 250) + "@test.com", // MaxLength is 256
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Email exceeds max length of 256");
    }

    [Fact]
    public async Task CreateCustomer_PhoneExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test",
            Phone = new string('1', 51), // MaxLength is 50
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Phone exceeds max length of 50");
    }

    [Fact]
    public async Task CreateCustomer_NotesExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test",
            Notes = new string('N', 2001), // MaxLength is 2000
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Notes exceeds max length of 2000");
    }

    #endregion

    #region Multi-Tenant Isolation Tests

    [Fact]
    public async Task GetCustomers_OnlyReturnsCustomersForTenant()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var customerA1 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Customer A1",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var customerA2 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Customer A2",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var customerB = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Name = "Customer B1",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.AddRange(customerA1, customerA2, customerB);
        await db.SaveChangesAsync();

        var tenantACustomers = db.Customers.Where(c => c.TenantId == tenantA).ToList();
        var tenantBCustomers = db.Customers.Where(c => c.TenantId == tenantB).ToList();

        tenantACustomers.Should().HaveCount(2);
        tenantACustomers.Should().Contain(c => c.Id == customerA1.Id);
        tenantACustomers.Should().Contain(c => c.Id == customerA2.Id);
        tenantACustomers.Should().NotContain(c => c.Id == customerB.Id);

        tenantBCustomers.Should().HaveCount(1);
        tenantBCustomers.Should().Contain(c => c.Id == customerB.Id);
        tenantBCustomers.Should().NotContain(c => c.Id == customerA1.Id);
    }

    #endregion

    #region Soft Delete Tests

    [Fact]
    public async Task DeactivateCustomer_SetsIsActiveToFalse()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Active Customer",
            Currency = "USD",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        customer.IsActive = false;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var saved = await db.Customers.FindAsync(customer.Id);
        saved.Should().NotBeNull();
        saved!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveCustomers_ExcludesInactiveCustomers()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var activeCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Active",
            Currency = "USD",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var inactiveCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Inactive",
            Currency = "USD",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.AddRange(activeCustomer, inactiveCustomer);
        await db.SaveChangesAsync();

        var activeOnly = db.Customers.Where(c => c.TenantId == tenantId && c.IsActive).ToList();

        activeOnly.Should().HaveCount(1);
        activeOnly.First().Id.Should().Be(activeCustomer.Id);
    }

    #endregion

    #region Email Uniqueness Tests

    [Fact]
    public async Task CreateCustomer_DuplicateEmailInSameTenant_ThrowsException()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();
        var email = "duplicate@test.com";

        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Customer 1",
            Email = email,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Customer 2",
            Email = email,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer1);
        await db.SaveChangesAsync();

        db.Customers.Add(customer2);
        var act = async () => await db.SaveChangesAsync();
        
        await act.Should().ThrowAsync<Exception>("Duplicate email in same tenant should fail");
    }

    [Fact]
    public async Task CreateCustomer_SameEmailInDifferentTenants_Succeeds()
    {
        await using var db = await CreateCleanContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var email = "shared@test.com";

        var customerA = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Customer A",
            Email = email,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var customerB = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Name = "Customer B",
            Email = email,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.AddRange(customerA, customerB);
        await db.SaveChangesAsync();

        var saved = db.Customers.Where(c => c.Email == email).ToList();
        saved.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateCustomer_NullEmailsAllowed_InSameTenant()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Customer 1",
            Email = null,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Customer 2",
            Email = null,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.AddRange(customer1, customer2);
        await db.SaveChangesAsync();

        var saved = db.Customers.Where(c => c.TenantId == tenantId && c.Email == null).ToList();
        saved.Should().HaveCount(2, "null emails should be allowed and not trigger uniqueness constraint");
    }

    #endregion

    #region Currency Field Tests

    [Fact]
    public async Task CreateCustomer_WithUSDCurrency_Succeeds()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "USD Customer",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var saved = await db.Customers.FindAsync(customer.Id);
        saved.Should().NotBeNull();
        saved!.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CreateCustomer_WithVariousCurrencies_AllSucceed()
    {
        await using var db = await CreateCleanContext();
        var tenantId = Guid.NewGuid();

        var currencies = new[] { "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "CNY" };

        foreach (var currency in currencies)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Customer {currency}",
                Currency = currency,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.Customers.Add(customer);
        }

        await db.SaveChangesAsync();

        var saved = db.Customers.Where(c => c.TenantId == tenantId).ToList();
        saved.Should().HaveCount(currencies.Length);
        saved.Select(c => c.Currency).Should().BeEquivalentTo(currencies);
    }

    [Fact]
    public async Task CreateCustomer_WithoutCurrency_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "No Currency",
            Currency = null!, // Required field
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Currency is required");
    }

    [Fact]
    public async Task CreateCustomer_CurrencyExceedsMaxLength_ThrowsException()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Invalid Currency",
            Currency = "USDD", // MaxLength is 3 (ISO 4217)
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("Currency exceeds max length of 3");
    }

    [Fact]
    public async Task UpdateCustomer_Currency_UpdatesSuccessfully()
    {
        await using var db = await CreateCleanContext();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Currency Change Test",
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        customer.Currency = "EUR";
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var saved = await db.Customers.FindAsync(customer.Id);
        saved.Should().NotBeNull();
        saved!.Currency.Should().Be("EUR");
    }

    #endregion
}
