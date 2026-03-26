using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

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

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct = default)
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value;
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var contact = await _contacts.GetContactByEmailAsync(email, ct);
        return contact == null ? NotFound() : Ok(contact);
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
        var existing = await _contacts.GetContactByIdAsync(id, ct);
        if (existing is null) return NotFound();

        // Portal users (non-admin/non-manager) can only update their own contact
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? User.FindFirst("email")?.Value;
            if (!string.Equals(existing.Email, userEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        var contact = await _contacts.UpdateContactAsync(id, request, ct);
        return contact is null ? NotFound() : Ok(contact);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken ct = default)
    {
        var deleted = await _contacts.DeleteContactAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
