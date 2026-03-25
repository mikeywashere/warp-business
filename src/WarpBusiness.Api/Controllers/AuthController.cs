using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WarpBusiness.Api.Identity;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "User");
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponse(
            token,
            user.Email!,
            user.FullName,
            roles,
            DateTimeOffset.UtcNow.AddMinutes(60)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized();

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized();

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponse(
            token,
            user.Email!,
            user.FullName,
            roles,
            DateTimeOffset.UtcNow.AddMinutes(60)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new AuthResponse(
            string.Empty,
            user.Email!,
            user.FullName,
            roles,
            DateTimeOffset.UtcNow));
    }

    [HttpGet("provider")]
    [AllowAnonymous]
    public IActionResult GetProvider([FromServices] IOptions<AuthProviderOptions> options)
    {
        var active = options.Value.ActiveProvider;
        var keycloak = options.Value.Keycloak;

        return Ok(new AuthProviderInfo(
            ActiveProvider: active.ToString(),
            KeycloakAuthUrl: active == AuthProviderType.Keycloak && !string.IsNullOrEmpty(keycloak.Authority)
                ? $"{keycloak.Authority}/protocol/openid-connect/auth"
                : null,
            SupportsLocalLogin: active == AuthProviderType.Local));
    }
}
