using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.TimeTracking.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Controllers;

[ApiController]
[Route("api/timetracking/billing-rates")]
[Authorize(Policy = "RequireActiveTenant")]
public class BillingRatesController : ControllerBase
{
    private readonly ICustomerBillingRateService _service;

    public BillingRatesController(ICustomerBillingRateService service)
    {
        _service = service;
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<ActionResult<List<CustomerBillingRateDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct = default)
    {
        var result = await _service.GetByEmployeeAsync(employeeId, ct);
        return Ok(result);
    }

    [HttpGet("company/{companyId}")]
    public async Task<ActionResult<List<CustomerBillingRateDto>>> GetByCompany(
        Guid companyId,
        CancellationToken ct = default)
    {
        var result = await _service.GetByCompanyAsync(companyId, ct);
        return Ok(result);
    }

    [HttpGet("employee/{employeeId}/company/{companyId}/current")]
    public async Task<ActionResult<CustomerBillingRateDto>> GetCurrentRate(
        Guid employeeId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var result = await _service.GetCurrentRateAsync(employeeId, companyId, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerBillingRateDto>> Create(
        [FromBody] CreateCustomerBillingRateRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = result.EmployeeId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CustomerBillingRateDto>> Update(
        Guid id,
        [FromBody] UpdateCustomerBillingRateRequest request,
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
