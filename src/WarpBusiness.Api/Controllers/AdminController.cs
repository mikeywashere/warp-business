using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Services;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmployeeUserService employeeUserService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = userManager.Users.ToList();
        var result = new List<UserSummaryDto>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserSummaryDto(
                user.Id,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.LastName}".Trim(),
                roles.ToList(),
                user.AuthProvider ?? "Local",
                user.LastLoginAt));
        }

        return Ok(result);
    }

    [HttpPost("users/{userId}/roles")]
    public async Task<IActionResult> SetUserRole(string userId, [FromBody] SetRoleRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (request.Add)
        {
            if (!await roleManager.RoleExistsAsync(request.Role))
                await roleManager.CreateAsync(new IdentityRole(request.Role));
            await userManager.AddToRoleAsync(user, request.Role);
        }
        else
        {
            await userManager.RemoveFromRoleAsync(user, request.Role);
        }

        return NoContent();
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // Prevent deleting the last admin
        var admins = await userManager.GetUsersInRoleAsync("Admin");
        if (admins.Count == 1 && admins.First().Id == userId)
            return Conflict(new { error = "Cannot delete the last admin user." });

        await userManager.DeleteAsync(user);
        return NoContent();
    }

    /// <summary>
    /// POST /api/admin/users/from-employee/{employeeId}
    /// Creates an ASP.NET Identity user account from an existing Employee record.
    /// Assigns the "User" role and links the account to the admin's active tenant.
    /// Returns a temporary password for the admin to share with the employee.
    /// </summary>
    [HttpPost("users/from-employee/{employeeId:guid}")]
    public async Task<ActionResult<CreateUserFromEmployeeResponse>> CreateUserFromEmployee(
        Guid employeeId, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { error = "Active tenant context is required. Select a tenant first." });

        var result = await employeeUserService.CreateUserFromEmployeeAsync(employeeId, tenantId, ct);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                CreateUserFromEmployeeError.EmployeeNotFound =>
                    NotFound(new { error = result.Error }),
                CreateUserFromEmployeeError.UserAlreadyExists =>
                    Conflict(new { error = result.Error }),
                _ =>
                    BadRequest(new { error = result.Error }),
            };
        }

        return Ok(new CreateUserFromEmployeeResponse(
            result.UserId!, result.Email!, result.FullName!,
            result.TemporaryPassword!, result.Roles!));
    }
}
