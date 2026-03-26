using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Crm.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companies;

    public CompaniesController(ICompanyService companies)
    {
        _companies = companies;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyDto>>> GetCompanies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await _companies.GetCompaniesAsync(page, pageSize, search, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> GetCompany(Guid id, CancellationToken ct = default)
    {
        var company = await _companies.GetCompanyByIdAsync(id, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> CreateCompany(CreateCompanyRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var company = await _companies.CreateCompanyAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, company);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> UpdateCompany(Guid id, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        var company = await _companies.UpdateCompanyAsync(id, request, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCompany(Guid id, CancellationToken ct = default)
    {
        var result = await _companies.DeleteCompanyAsync(id, ct);
        return result switch
        {
            DeleteCompanyResult.Deleted => NoContent(),
            DeleteCompanyResult.NotFound => NotFound(),
            DeleteCompanyResult.HasContacts => Conflict(new { message = "Company has associated contacts and cannot be deleted." }),
            _ => StatusCode(500)
        };
    }
}
