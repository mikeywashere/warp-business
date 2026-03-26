using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activities;

    public ActivitiesController(IActivityService activities)
    {
        _activities = activities;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? contactId = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] Guid? dealId = null,
        [FromQuery] bool? isCompleted = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        return Ok(await _activities.GetActivitiesAsync(page, pageSize, contactId, companyId, dealId, isCompleted, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityDto>> GetActivity(Guid id, CancellationToken ct = default)
    {
        var activity = await _activities.GetActivityAsync(id, ct);
        return activity is null ? NotFound() : Ok(activity);
    }

    [HttpPost]
    public async Task<ActionResult<ActivityDto>> CreateActivity(CreateActivityRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var activity = await _activities.CreateActivityAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, activity);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActivityDto>> UpdateActivity(Guid id, UpdateActivityRequest request, CancellationToken ct = default)
    {
        var activity = await _activities.UpdateActivityAsync(id, request, ct);
        return activity is null ? NotFound() : Ok(activity);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteActivity(Guid id, CancellationToken ct = default)
    {
        var completed = await _activities.CompleteActivityAsync(id, ct);
        return completed ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteActivity(Guid id, CancellationToken ct = default)
    {
        var deleted = await _activities.DeleteActivityAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
