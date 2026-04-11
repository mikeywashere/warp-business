using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Tests.Helpers;

namespace WarpBusiness.Api.Tests.Data;

public class WarpBusinessDbContextTests : IDisposable
{
    private readonly WarpBusinessDbContext _db;

    public WarpBusinessDbContextTests()
    {
        _db = TestHelpers.CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Users_CanCreate_AndRetrieve()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Jean-Luc",
            LastName = "Picard",
            Email = "picard@enterprise.com",
            Role = UserRole.SystemAdministrator
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var found = await _db.Users.FindAsync(user.Id);
        found.Should().NotBeNull();
        found!.Email.Should().Be("picard@enterprise.com");
        found.Role.Should().Be(UserRole.SystemAdministrator);
    }

    [Fact]
    public async Task Users_CanUpdate()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "William",
            LastName = "Riker",
            Email = "riker@enterprise.com"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        user.FirstName = "Will";
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var found = await _db.Users.FindAsync(user.Id);
        found!.FirstName.Should().Be("Will");
    }

    [Fact]
    public async Task Users_CanDelete()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Tasha",
            LastName = "Yar",
            Email = "yar@enterprise.com"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        var found = await _db.Users.FindAsync(user.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task Tenants_CanCrud()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Starfleet Command",
            Slug = "starfleet-command",
            IsActive = true
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var found = await _db.Tenants.FindAsync(tenant.Id);
        found.Should().NotBeNull();
        found!.Slug.Should().Be("starfleet-command");

        _db.Tenants.Remove(found);
        await _db.SaveChangesAsync();

        (await _db.Tenants.FindAsync(tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task UserTenantMembership_CompositeKey_Works()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Data",
            LastName = "Soong",
            Email = "data@enterprise.com"
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Engineering",
            Slug = "engineering"
        };

        _db.Users.Add(user);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var membership = new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            JoinedAt = DateTime.UtcNow
        };

        _db.UserTenantMemberships.Add(membership);
        await _db.SaveChangesAsync();

        var found = await _db.UserTenantMemberships.FindAsync(user.Id, tenant.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task UserRole_StringConversion_WorksCorrectly()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Geordi",
            LastName = "La Forge",
            Email = "laforge@enterprise.com",
            Role = UserRole.SystemAdministrator
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var found = await _db.Users.FindAsync(user.Id);
        found!.Role.Should().Be(UserRole.SystemAdministrator);
        found.Role.ToString().Should().Be("SystemAdministrator");
    }

    [Fact]
    public async Task CascadeDelete_User_RemovesMemberships()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Deanna",
            LastName = "Troi",
            Email = "troi@enterprise.com"
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Counseling",
            Slug = "counseling"
        };

        _db.Users.Add(user);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        _db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenant.Id
        });
        await _db.SaveChangesAsync();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        var memberships = await _db.UserTenantMemberships
            .Where(m => m.UserId == user.Id)
            .ToListAsync();

        memberships.Should().BeEmpty("cascade delete should remove memberships when user is deleted");
    }

    [Fact]
    public async Task CascadeDelete_Tenant_RemovesMemberships()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Beverly",
            LastName = "Crusher",
            Email = "crusher@enterprise.com"
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Medical",
            Slug = "medical"
        };

        _db.Users.Add(user);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        _db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenant.Id
        });
        await _db.SaveChangesAsync();

        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync();

        var memberships = await _db.UserTenantMemberships
            .Where(m => m.TenantId == tenant.Id)
            .ToListAsync();

        memberships.Should().BeEmpty("cascade delete should remove memberships when tenant is deleted");
    }
}
