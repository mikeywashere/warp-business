using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Api.Identity;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : ControllerBase
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
}
