using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Invoicing.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Controllers;

[ApiController]
[Route("api/invoicing/invoices/{invoiceId:guid}/payments")]
[Authorize(Policy = "RequireActiveTenant")]
public class InvoicePaymentsController : ControllerBase
{
    private readonly IInvoicePaymentService _service;
    private readonly ILogger<InvoicePaymentsController> _logger;

    public InvoicePaymentsController(IInvoicePaymentService service, ILogger<InvoicePaymentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoicePaymentDto>>> GetPayments(
        Guid invoiceId, CancellationToken ct = default)
    {
        var result = await _service.GetPaymentsAsync(invoiceId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InvoicePaymentDto>> RecordPayment(
        Guid invoiceId,
        [FromBody] CreateInvoicePaymentRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        try
        {
            var result = await _service.RecordPaymentAsync(invoiceId, request, userId, ct);
            return CreatedAtAction(nameof(GetPayments), new { invoiceId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{paymentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePayment(
        Guid invoiceId, Guid paymentId, CancellationToken ct = default)
    {
        var success = await _service.DeletePaymentAsync(invoiceId, paymentId, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
