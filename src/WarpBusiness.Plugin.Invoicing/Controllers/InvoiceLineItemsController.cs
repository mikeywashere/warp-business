using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Invoicing.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Controllers;

[ApiController]
[Route("api/invoicing/invoices/{invoiceId:guid}/line-items")]
[Authorize(Policy = "RequireActiveTenant")]
public class InvoiceLineItemsController : ControllerBase
{
    private readonly IInvoiceLineItemService _service;
    private readonly ILogger<InvoiceLineItemsController> _logger;

    public InvoiceLineItemsController(IInvoiceLineItemService service, ILogger<InvoiceLineItemsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceLineItemDto>>> GetLineItems(
        Guid invoiceId, CancellationToken ct = default)
    {
        var result = await _service.GetLineItemsAsync(invoiceId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceLineItemDto>> AddLineItem(
        Guid invoiceId,
        [FromBody] CreateInvoiceLineItemRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _service.AddLineItemAsync(invoiceId, request, ct);
            return CreatedAtAction(nameof(GetLineItems), new { invoiceId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{lineItemId:guid}")]
    public async Task<IActionResult> UpdateLineItem(
        Guid invoiceId, Guid lineItemId,
        [FromBody] UpdateInvoiceLineItemRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.UpdateLineItemAsync(invoiceId, lineItemId, request, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{lineItemId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteLineItem(
        Guid invoiceId, Guid lineItemId, CancellationToken ct = default)
    {
        var success = await _service.DeleteLineItemAsync(invoiceId, lineItemId, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderLineItems(
        Guid invoiceId,
        [FromBody] IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default)
    {
        var success = await _service.ReorderLineItemsAsync(invoiceId, orderedIds, ct);
        if (!success) return Conflict(new { message = "Invoice not found or not in Draft status." });
        return NoContent();
    }
}
