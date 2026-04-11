using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;

namespace WarpBusiness.Api.Tests.Data;

[Collection("Database")]
public class DbInitializerTests
{
    private readonly PostgreSqlFixture _fixture;

    public DbInitializerTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<WarpBusinessDbContext> CreateMigratedContext()
    {
        var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();
        return db;
    }

    private DbInitializer CreateInitializer(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DbInitializer>>();
        return new DbInitializer(serviceProvider, logger);
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<WarpBusinessDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString));
        services.AddLogging(b => b.AddConsole());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Seed_CreatesDefaultTenant_Warp()
    {
        await using var db = await CreateMigratedContext();
        // Clear existing data for isolation
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();

        using var sp = BuildServiceProvider();
        var initializer = CreateInitializer(sp);
        await initializer.StartAsync(CancellationToken.None);

        await using var verifyDb = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        var tenant = await verifyDb.Tenants.FirstOrDefaultAsync(t => t.Slug == "warp");

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Warp");
        tenant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Seed_CreatesAdminUser_MichaelSchmidt()
    {
        await using var db = await CreateMigratedContext();
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();

        using var sp = BuildServiceProvider();
        var initializer = CreateInitializer(sp);
        await initializer.StartAsync(CancellationToken.None);

        await using var verifyDb = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        var admin = await verifyDb.Users.FirstOrDefaultAsync(u => u.Email == "mikenging@hotmail.com");

        admin.Should().NotBeNull();
        admin!.FirstName.Should().Be("Michael");
        admin.LastName.Should().Be("Schmidt");
        admin.Role.Should().Be(UserRole.SystemAdministrator);
    }

    [Fact]
    public async Task Seed_AddsAdminToDefaultTenant()
    {
        await using var db = await CreateMigratedContext();
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();

        using var sp = BuildServiceProvider();
        var initializer = CreateInitializer(sp);
        await initializer.StartAsync(CancellationToken.None);

        await using var verifyDb = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        var admin = await verifyDb.Users.FirstOrDefaultAsync(u => u.Email == "mikenging@hotmail.com");
        var tenant = await verifyDb.Tenants.FirstOrDefaultAsync(t => t.Slug == "warp");

        admin.Should().NotBeNull();
        tenant.Should().NotBeNull();

        var membership = await verifyDb.UserTenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == admin!.Id && m.TenantId == tenant!.Id);

        membership.Should().NotBeNull("admin should be a member of the default tenant");
    }

    [Fact]
    public async Task Seed_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await using var db = await CreateMigratedContext();
        db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
        db.Users.RemoveRange(db.Users);
        db.Tenants.RemoveRange(db.Tenants);
        await db.SaveChangesAsync();

        using var sp = BuildServiceProvider();
        var initializer = CreateInitializer(sp);

        // Run seed twice
        await initializer.StartAsync(CancellationToken.None);
        await initializer.StartAsync(CancellationToken.None);

        await using var verifyDb = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        var tenantCount = await verifyDb.Tenants.CountAsync(t => t.Slug == "warp");
        var userCount = await verifyDb.Users.CountAsync(u => u.Email == "mikenging@hotmail.com");

        tenantCount.Should().Be(1, "seed should not create duplicate tenants");
        userCount.Should().Be(1, "seed should not create duplicate users");
    }
}
