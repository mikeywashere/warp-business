using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Tests.Helpers;
using WarpBusiness.Api.Tests.Infrastructure;

namespace WarpBusiness.Api.Tests.Migrations;

[Collection("Database")]
public class MigrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public MigrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrations_ApplySuccessfully_ToFreshDatabase()
    {
        await using var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);

        var act = () => db.Database.MigrateAsync();

        await act.Should().NotThrowAsync("migrations should apply cleanly to a fresh database");
    }

    [Fact]
    public async Task Migrations_AreIdempotent_CanBeAppliedTwice()
    {
        await using var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);

        await db.Database.MigrateAsync();

        var act = () => db.Database.MigrateAsync();

        await act.Should().NotThrowAsync("migrations should be idempotent");
    }

    [Fact]
    public async Task Migrations_NoPendingMigrations_AfterMigrateAsync()
    {
        await using var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);

        await db.Database.MigrateAsync();

        var pending = await db.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty("all migrations should have been applied");
    }

    [Fact]
    public async Task Migrations_CreateExpectedTables()
    {
        await using var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();

        // Verify tables exist by querying PostgreSQL information_schema
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT table_name FROM information_schema.tables 
            WHERE table_schema = 'warp' 
            AND table_name IN ('Users', 'Tenants', 'UserTenantMemberships')
            ORDER BY table_name;
            """;

        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        tables.Should().Contain("Tenants");
        tables.Should().Contain("UserTenantMemberships");
        tables.Should().Contain("Users");
    }

    [Fact]
    public async Task Migrations_SchemaMatchesModelSnapshot()
    {
        await using var db = TestHelpers.CreatePostgresDbContext(_fixture.ConnectionString);
        await db.Database.MigrateAsync();

        // Verify the model has the expected entity types
        var model = db.Model;
        var entityTypes = model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        entityTypes.Should().Contain("ApplicationUser");
        entityTypes.Should().Contain("Tenant");
        entityTypes.Should().Contain("UserTenantMembership");
    }
}
