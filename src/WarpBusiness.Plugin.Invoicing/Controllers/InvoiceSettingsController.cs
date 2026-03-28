using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Invoicing.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Controllers;

[ApiController]
[Route("api/invoicing/settings")]
[Authorize(Roles = "Admin")]
public class InvoiceSettingsController : ControllerBase
{
    private readonly IInvoiceSettingsService _service;
    private readonly ILogger<InvoiceSettingsController> _logger;

    public InvoiceSettingsController(IInvoiceSettingsService service, ILogger<InvoiceSettingsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<InvoiceSettingsDto>> GetSettings(CancellationToken ct = default)
    {
        var result = await _service.GetSettingsAsync(ct);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<InvoiceSettingsDto>> UpdateSettings(
        [FromBody] UpdateInvoiceSettingsRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? string.Empty;

        var result = await _service.UpdateSettingsAsync(request, userId, ct);
        return Ok(result);
    }
}
