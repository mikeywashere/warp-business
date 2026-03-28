using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[Authorize(Policy = "RequireActiveTenant")]
[ApiController]
[Route("api/contacts/{contactId:guid}/relationships")]
public class ContactEmployeeRelationshipsController : ControllerBase
{
    private readonly IContactEmployeeRelationshipService _relationships;

    public ContactEmployeeRelationshipsController(IContactEmployeeRelationshipService relationships)
    {
        _relationships = relationships;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactEmployeeRelationshipDto>>> GetRelationships(Guid contactId, CancellationToken ct = default) =>
        Ok(await _relationships.GetRelationshipsForContactAsync(contactId, ct));

    [HttpPost]
    public async Task<ActionResult<ContactEmployeeRelationshipDto>> Create(
        Guid contactId,
        CreateContactEmployeeRelationshipRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var relationship = await _relationships.AddRelationshipAsync(contactId, request, userId, ct);
        return relationship is null
            ? NotFound()
            : CreatedAtAction(nameof(GetRelationships), new { contactId }, relationship);
    }

    [HttpDelete("{relationshipId:guid}")]
    public async Task<IActionResult> Delete(Guid contactId, Guid relationshipId, CancellationToken ct = default)
    {
        var deleted = await _relationships.RemoveRelationshipAsync(contactId, relationshipId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
