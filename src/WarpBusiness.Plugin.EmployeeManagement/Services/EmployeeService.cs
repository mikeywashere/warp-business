using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.EmployeeManagement.Data;
using WarpBusiness.Plugin.EmployeeManagement.Domain;

namespace WarpBusiness.Plugin.EmployeeManagement.Services;

public class EmployeeService(EmployeeDbContext db) : IEmployeeService
{
    public async Task<(List<Employee> Items, int Total)> GetPagedAsync(
        int page, int pageSize, bool includeInactive = false,
        string? department = null, string? search = null)
    {
        var query = db.Employees.AsQueryable();

        if (!includeInactive)
            query = query.Where(e => e.IsActive);

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.Department == department);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e =>
                e.FirstName.Contains(search) ||
                e.LastName.Contains(search) ||
                e.Email.Contains(search) ||
                (e.JobTitle != null && e.JobTitle.Contains(search)));

        var total = await query.CountAsync();
        var items = await query
            .Include(e => e.Manager)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Employee?> GetByIdAsync(Guid id) =>
        await db.Employees.Include(e => e.Manager).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Employee> CreateAsync(Employee employee)
    {
        employee.Email = employee.Email.ToLowerInvariant();
        employee.CreatedAt = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    public async Task<Employee?> UpdateAsync(Guid id, Employee updated)
    {
        var existing = await db.Employees.FindAsync(id);
        if (existing is null) return null;

        existing.FirstName = updated.FirstName;
        existing.LastName = updated.LastName;
        existing.Email = updated.Email?.ToLowerInvariant() ?? string.Empty;
        existing.Phone = updated.Phone;
        existing.Department = updated.Department;
        existing.JobTitle = updated.JobTitle;
        existing.HireDate = updated.HireDate;
        existing.TerminationDate = updated.TerminationDate;
        existing.IsActive = updated.IsActive;
        existing.ManagerId = updated.ManagerId;
        existing.Notes = updated.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeactivateAsync(Guid id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return false;
        employee.IsActive = false;
        employee.TerminationDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
        employee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return false;
        db.Employees.Remove(employee);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetDepartmentsAsync() =>
        await db.Employees
            .Where(e => e.Department != null)
            .Select(e => e.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

    public async Task<List<Employee>> GetManagerCandidatesAsync() =>
        await db.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync();
}
