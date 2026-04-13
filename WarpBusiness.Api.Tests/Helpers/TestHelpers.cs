using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Employees.Data;

namespace WarpBusiness.Api.Tests.Helpers;

public static class TestHelpers
{
    /// <summary>
    /// Creates a ClaimsPrincipal representing a SystemAdministrator with app_role and realm_access claims.
    /// </summary>
    public static ClaimsPrincipal CreateAdminPrincipal(
        string sub = "admin-sub-id",
        string email = "admin@warpindustries.com")
    {
        var claims = new List<Claim>
        {
            new("sub", sub),
            new("email", email),
            new("app_role", "SystemAdministrator"),
            new("realm_access", "{\"roles\":[\"system-administrator\"]}")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal representing a regular user.
    /// </summary>
    public static ClaimsPrincipal CreateUserPrincipal(
        string email = "user@warpindustries.com",
        string sub = "user-sub-id")
    {
        var claims = new List<Claim>
        {
            new("sub", sub),
            new("email", email),
            new("app_role", "User")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a WarpBusinessDbContext configured for PostgreSQL with the given connection string.
    /// </summary>
    public static WarpBusinessDbContext CreatePostgresDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WarpBusinessDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WarpBusinessDbContext(options);
    }

    /// <summary>
    /// Creates a WarpBusinessDbContext using the InMemory provider for fast unit tests.
    /// </summary>
    public static WarpBusinessDbContext CreateInMemoryDbContext(string? databaseName = null)
    {
        databaseName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<WarpBusinessDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new WarpBusinessDbContext(options);
    }

    /// <summary>
    /// Creates an EmployeeDbContext configured for PostgreSQL with the given connection string.
    /// </summary>
    public static EmployeeDbContext CreatePostgresEmployeeDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<EmployeeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EmployeeDbContext(options);
    }
}
