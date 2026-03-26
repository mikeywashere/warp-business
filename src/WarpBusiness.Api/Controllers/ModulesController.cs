using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Api.Plugins;
using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModulesController(ModuleRegistry registry) : ControllerBase
{
    /// <summary>Returns metadata for all discovered modules (loaded and failed).</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<ModuleInfo>> GetModules() => Ok(registry.ModuleInfos);

    /// <summary>Returns nav items from all loaded modules for the current user.</summary>
    [HttpGet("nav-items")]
    public ActionResult<IReadOnlyList<ModuleNavItem>> GetNavItems() => Ok(registry.GetAllNavItems());
}
