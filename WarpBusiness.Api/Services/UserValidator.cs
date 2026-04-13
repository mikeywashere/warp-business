using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Employees.Services;

namespace WarpBusiness.Api.Services;

public class UserValidator(WarpBusinessDbContext db) : IUserValidator
{
    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<bool> UserBelongsToTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await db.UserTenantMemberships.AnyAsync(
            m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);
    }
}
