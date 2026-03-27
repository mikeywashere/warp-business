using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[Authorize(Policy = "RequireActiveTenant")]
[ApiController]
[Route("api/[controller]")]
public class DealsController : ControllerBase
{
    private readonly IDealService _deals;

    public DealsController(IDealService deals)
    {
        _deals = deals;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DealDto>>> GetDeals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? stage = null,
        CancellationToken ct = default)
    {
        return Ok(await _deals.GetDealsAsync(page, pageSize, search, stage, ct));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DealPipelineSummary>> GetSummary(CancellationToken ct = default)
    {
        return Ok(await _deals.GetDealSummaryAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DealDto>> GetDeal(Guid id, CancellationToken ct = default)
    {
        var deal = await _deals.GetDealByIdAsync(id, ct);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    public async Task<ActionResult<DealDto>> CreateDeal(CreateDealRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var deal = await _deals.CreateDealAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetDeal), new { id = deal.Id }, deal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DealDto>> UpdateDeal(Guid id, UpdateDealRequest request, CancellationToken ct = default)
    {
        var deal = await _deals.UpdateDealAsync(id, request, ct);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDeal(Guid id, CancellationToken ct = default)
    {
        var deleted = await _deals.DeleteDealAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
