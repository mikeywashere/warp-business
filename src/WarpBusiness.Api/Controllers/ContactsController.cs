using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Api.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contacts;

    public ContactsController(IContactService contacts)
    {
        _contacts = contacts;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ContactDto>>> GetContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await _contacts.GetContactsAsync(page, pageSize, search, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactDto>> GetContact(Guid id, CancellationToken ct = default)
    {
        var contact = await _contacts.GetContactByIdAsync(id, ct);
        return contact is null ? NotFound() : Ok(contact);
    }

    [HttpPost]
    public async Task<ActionResult<ContactDto>> CreateContact(CreateContactRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var contact = await _contacts.CreateContactAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetContact), new { id = contact.Id }, contact);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContactDto>> UpdateContact(Guid id, UpdateContactRequest request, CancellationToken ct = default)
    {
        var contact = await _contacts.UpdateContactAsync(id, request, ct);
        return contact is null ? NotFound() : Ok(contact);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken ct = default)
    {
        var deleted = await _contacts.DeleteContactAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
