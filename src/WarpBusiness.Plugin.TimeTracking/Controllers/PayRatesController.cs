using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.TimeTracking.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Controllers;

[ApiController]
[Route("api/timetracking/pay-rates")]
[Authorize(Policy = "RequireActiveTenant")]
public class PayRatesController : ControllerBase
{
    private readonly IEmployeePayRateService _service;

    public PayRatesController(IEmployeePayRateService service)
    {
        _service = service;
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<ActionResult<List<EmployeePayRateDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct = default)
    {
        var result = await _service.GetByEmployeeAsync(employeeId, ct);
        return Ok(result);
    }

    [HttpGet("employee/{employeeId}/current")]
    public async Task<ActionResult<EmployeePayRateDto>> GetCurrentRate(
        Guid employeeId,
        CancellationToken ct = default)
    {
        var result = await _service.GetCurrentRateAsync(employeeId, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeePayRateDto>> Create(
        [FromBody] CreateEmployeePayRateRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = result.EmployeeId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeePayRateDto>> Update(
        Guid id,
        [FromBody] UpdateEmployeePayRateRequest request,
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
