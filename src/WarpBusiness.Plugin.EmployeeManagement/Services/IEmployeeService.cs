using WarpBusiness.Plugin.EmployeeManagement.Domain;

namespace WarpBusiness.Plugin.EmployeeManagement.Services;

public interface IEmployeeService
{
    Task<(List<Employee> Items, int Total)> GetPagedAsync(int page, int pageSize, bool includeInactive = false, string? department = null, string? search = null);
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee> CreateAsync(Employee employee);
    Task<Employee?> UpdateAsync(Guid id, Employee updated);
    Task<bool> DeactivateAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
    Task<List<string>> GetDepartmentsAsync();
    Task<List<Employee>> GetManagerCandidatesAsync();
}
