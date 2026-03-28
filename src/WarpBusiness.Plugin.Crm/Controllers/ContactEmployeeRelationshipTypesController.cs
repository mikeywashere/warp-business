using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[Authorize(Policy = "RequireActiveTenant")]
[ApiController]
[Route("api/contact-relationship-types")]
public class ContactEmployeeRelationshipTypesController : ControllerBase
{
    private readonly IContactEmployeeRelationshipService _relationships;

    public ContactEmployeeRelationshipTypesController(IContactEmployeeRelationshipService relationships)
    {
        _relationships = relationships;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactEmployeeRelationshipTypeDto>>> Get(CancellationToken ct = default) =>
        Ok(await _relationships.GetRelationshipTypesAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ContactEmployeeRelationshipTypeDto>> Create(
        CreateContactEmployeeRelationshipTypeRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var type = await _relationships.CreateRelationshipTypeAsync(request, userId, ct);
        return CreatedAtAction(nameof(Get), new { }, type);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ContactEmployeeRelationshipTypeDto>> Update(
        Guid id,
        UpdateContactEmployeeRelationshipTypeRequest request,
        CancellationToken ct = default)
    {
        var updated = await _relationships.UpdateRelationshipTypeAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await _relationships.DeleteRelationshipTypeAsync(id, ct);
        return result switch
        {
            DeleteContactEmployeeRelationshipTypeResult.Deleted => NoContent(),
            DeleteContactEmployeeRelationshipTypeResult.NotFound => NotFound(),
            DeleteContactEmployeeRelationshipTypeResult.InUse => Conflict(new { message = "The relationship type is in use." }),
            _ => StatusCode(500)
        };
    }
}
