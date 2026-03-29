using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Identity.Tenancy;
using WarpBusiness.Plugin.EmployeeManagement.Data;

namespace WarpBusiness.Api.Services;

public class EmployeeUserService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    EmployeeDbContext employeeDb) : IEmployeeUserService
{
    public async Task<CreateUserFromEmployeeResult> CreateUserFromEmployeeAsync(
        Guid employeeId, Guid tenantId, CancellationToken ct = default)
    {
        // 1. Look up employee (tenant query filter scopes to caller's tenant)
        var employee = await employeeDb.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        if (employee is null)
            return Fail(CreateUserFromEmployeeError.EmployeeNotFound, "Employee not found.");

        if (!employee.IsActive)
            return Fail(CreateUserFromEmployeeError.EmployeeNotFound, "Employee is inactive.");

        // 2. Check no existing user with that email
        var existingUser = await userManager.FindByEmailAsync(employee.Email);
        if (existingUser is not null)
            return Fail(CreateUserFromEmployeeError.UserAlreadyExists,
                $"A user account already exists for {employee.Email}.");

        // 3. Generate temporary password
        var tempPassword = GenerateTemporaryPassword();

        // 4. Create ApplicationUser
        var user = new ApplicationUser
        {
            UserName = employee.Email,
            Email = employee.Email,
            EmailConfirmed = true,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            AuthProvider = "Local",
        };

        var createResult = await userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
            return Fail(CreateUserFromEmployeeError.CreationFailed,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));

        // 5. Add to "User" role
        await userManager.AddToRoleAsync(user, "User");

        // 6. Create UserTenant record linking the user to the employee's tenant
        db.UserTenants.Add(new UserTenant
        {
            UserId = user.Id,
            TenantId = tenantId,
            Role = "Member",
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(user);

        return new CreateUserFromEmployeeResult
        {
            Success = true,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            TemporaryPassword = tempPassword,
            Roles = roles.ToList(),
        };
    }

    private static CreateUserFromEmployeeResult Fail(CreateUserFromEmployeeError code, string error) =>
        new() { Success = false, ErrorCode = code, Error = error };

    /// <summary>
    /// Generates a 16-character password that meets the Identity password policy:
    /// at least one uppercase letter, one lowercase letter, and one digit.
    /// Uses cryptographic RNG — no ambiguous characters (0/O, 1/l/I).
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string allChars = upper + lower + digits;

        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[16];
        // Guarantee at least one of each required category
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];

        for (int i = 3; i < 16; i++)
            chars[i] = allChars[bytes[i] % allChars.Length];

        // Fisher-Yates shuffle
        var shuffleBytes = new byte[16];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
