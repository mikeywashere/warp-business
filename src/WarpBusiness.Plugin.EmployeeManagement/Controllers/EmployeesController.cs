using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.EmployeeManagement.Domain;
using WarpBusiness.Plugin.EmployeeManagement.Services;

namespace WarpBusiness.Plugin.EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? department = null,
        [FromQuery] string? search = null)
    {
        var (items, total) = await employees.GetPagedAsync(page, pageSize, includeInactive, department, search);
        return Ok(new { Items = items.Select(ToDto), Total = total, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var employee = await employees.GetByIdAsync(id);
        return employee is null ? NotFound() : Ok(ToDto(employee));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] EmployeeRequest request)
    {
        var employee = FromRequest(request);
        var created = await employees.CreateAsync(employee);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDto(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EmployeeRequest request)
    {
        var updated = await employees.UpdateAsync(id, FromRequest(request));
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var success = await employees.DeactivateAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await employees.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments() =>
        Ok(await employees.GetDepartmentsAsync());

    [HttpGet("managers")]
    public async Task<IActionResult> GetManagers() =>
        Ok((await employees.GetManagerCandidatesAsync()).Select(ToDto));

    private static object ToDto(Employee e) => new
    {
        e.Id,
        e.FirstName,
        e.LastName,
        FullName = $"{e.FirstName} {e.LastName}",
        e.Email,
        e.Phone,
        e.Department,
        e.JobTitle,
        e.HireDate,
        e.TerminationDate,
        e.IsActive,
        e.ManagerId,
        ManagerName = e.Manager is null ? null : $"{e.Manager.FirstName} {e.Manager.LastName}",
        e.Notes,
        e.CreatedAt,
        e.UpdatedAt
    };

    private static Employee FromRequest(EmployeeRequest r) => new()
    {
        FirstName = r.FirstName,
        LastName = r.LastName,
        Email = r.Email,
        Phone = r.Phone,
        Department = r.Department,
        JobTitle = r.JobTitle,
        HireDate = r.HireDate,
        TerminationDate = r.TerminationDate,
        IsActive = r.IsActive,
        ManagerId = r.ManagerId,
        Notes = r.Notes
    };
}

public record EmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Department,
    string? JobTitle,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsActive,
    Guid? ManagerId,
    string? Notes
);
