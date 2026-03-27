using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[ApiController]
[Route("api/custom-fields")]
[Authorize(Policy = "RequireActiveTenant")]
public class CustomFieldsController : ControllerBase
{
    private readonly ICustomFieldService _customFields;
    private readonly CrmDbContext _db;

    public CustomFieldsController(ICustomFieldService customFields, CrmDbContext db)
    {
        _customFields = customFields;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> GetDefinitions(
        [FromQuery] string entityType = "Contact",
        CancellationToken ct = default)
    {
        return Ok(await _customFields.GetDefinitionsAsync(entityType, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomFieldDefinitionDto>> GetDefinition(Guid id, CancellationToken ct = default)
    {
        var def = await _customFields.GetDefinitionAsync(id, ct);
        return def is null ? NotFound() : Ok(def);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomFieldDefinitionDto>> CreateDefinition(
        CreateCustomFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.CustomFieldDefinitions
            .AnyAsync(d => d.Name == request.Name && d.EntityType == request.EntityType, ct);
        if (duplicate)
            return Conflict(new { message = "A custom field with this name already exists for this entity type." });

        var def = await _customFields.CreateDefinitionAsync(request, ct);
        return CreatedAtAction(nameof(GetDefinition), new { id = def.Id }, def);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomFieldDefinitionDto>> UpdateDefinition(
        Guid id, UpdateCustomFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var def = await _customFields.UpdateDefinitionAsync(id, request, ct);
        return def is null ? NotFound() : Ok(def);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken ct = default)
    {
        var hasValues = await _db.CustomFieldValues
            .AnyAsync(v => v.FieldDefinitionId == id, ct);

        if (hasValues)
            return Conflict(new { message = "Cannot delete a field that has values. Remove all values first or deactivate the field." });

        var deleted = await _customFields.DeleteDefinitionAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
