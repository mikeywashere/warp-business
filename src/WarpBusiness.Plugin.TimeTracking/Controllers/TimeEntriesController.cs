using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.TimeTracking.Services;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Controllers;

[ApiController]
[Route("api/timetracking/entries")]
[Authorize(Policy = "RequireActiveTenant")]
public class TimeEntriesController : ControllerBase
{
    private readonly ITimeEntryService _service;

    public TimeEntriesController(ITimeEntryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TimeEntryDto>>> GetEntries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetEntriesAsync(page, pageSize, employeeId, companyId, status, fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TimeEntryDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TimeEntryDto>> Create(
        [FromBody] CreateTimeEntryRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TimeEntryDto>> Update(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
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

    [HttpPost("{id}/submit")]
    public async Task<ActionResult<TimeEntryDto>> Submit(Guid id, CancellationToken ct = default)
    {
        var result = await _service.SubmitAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<TimeEntryDto>> Approve(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.ApproveAsync(id, userId, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<TimeEntryDto>> Reject(
        Guid id,
        [FromBody] RejectTimeEntryRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.RejectAsync(id, userId, request.Reason, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }
}

public record RejectTimeEntryRequest(string Reason);
