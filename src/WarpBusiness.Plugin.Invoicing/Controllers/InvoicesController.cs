using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Invoicing.Services;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Controllers;

[ApiController]
[Route("api/invoicing/invoices")]
[Authorize(Policy = "RequireActiveTenant")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IInvoiceService service, ILogger<InvoicesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> GetInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetInvoicesAsync(page, pageSize, search, companyId, status, fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<InvoiceSummaryDto>> GetSummary(CancellationToken ct = default)
    {
        var result = await _service.GetSummaryAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDetailDto>> GetInvoice(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetInvoiceAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.CreateInvoiceAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetInvoice), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateInvoice(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.UpdateInvoiceAsync(id, request, ct);
        if (result == null) return Conflict(new { message = "Invoice not found or not in Draft status." });
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteInvoice(Guid id, CancellationToken ct = default)
    {
        var result = await _service.DeleteInvoiceAsync(id, ct);
        return result switch
        {
            DeleteInvoiceResult.Deleted => NoContent(),
            DeleteInvoiceResult.NotFound => NotFound(),
            DeleteInvoiceResult.NotDraft => Conflict(new { message = "Only draft invoices can be deleted." }),
            _ => StatusCode(500)
        };
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> SendInvoice(Guid id, CancellationToken ct = default)
    {
        var result = await _service.SendInvoiceAsync(id, ct);
        if (result == null) return Conflict(new { message = "Invoice not found, not in Draft status, or has no line items." });
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelInvoice(Guid id, CancellationToken ct = default)
    {
        var result = await _service.CancelInvoiceAsync(id, ct);
        if (result == null) return Conflict(new { message = "Invoice not found or cannot be cancelled from its current status." });
        return Ok(result);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> VoidInvoice(Guid id, CancellationToken ct = default)
    {
        var result = await _service.VoidInvoiceAsync(id, ct);
        if (result == null) return Conflict(new { message = "Invoice not found or cannot be voided from its current status." });
        return Ok(result);
    }
}
