using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WarpBusiness.Api.Filters;

/// <summary>
/// Asserts the JWT <c>tenant_id</c> claim matches the <c>{tenantId}</c> route parameter.
/// Prevents cross-tenant data access via URL manipulation (IDOR protection at the tenant boundary).
///
/// Apply to controllers or actions where tenantId is in the route:
///   [RequireTenantRouteMatch]
///   public IActionResult GetSomething(Guid tenantId) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireTenantRouteMatchAttribute : Attribute, IActionFilter
{
    private const string RouteParamName = "tenantId";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var routeTenantId = context.RouteData.Values[RouteParamName]?.ToString();
        if (string.IsNullOrEmpty(routeTenantId))
            return; // No tenantId route param — nothing to guard

        var jwtTenantId = context.HttpContext.User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(jwtTenantId))
        {
            context.Result = new ObjectResult(new { error = "No active tenant in token" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!string.Equals(routeTenantId, jwtTenantId, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new { error = "Cross-tenant access denied" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
