using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.TimeTracking.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Controllers;

[ApiController]
[Route("api/timetracking/entry-types")]
[Authorize(Policy = "RequireActiveTenant")]
public class TimeEntryTypesController : ControllerBase
{
    private readonly ITimeEntryTypeService _service;

    public TimeEntryTypesController(ITimeEntryTypeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TimeEntryTypeDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(includeInactive, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TimeEntryTypeDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TimeEntryTypeDto>> Create(
        [FromBody] CreateTimeEntryTypeRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TimeEntryTypeDto>> Update(
        Guid id,
        [FromBody] UpdateTimeEntryTypeRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var success = await _service.DeleteAsync(id, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
