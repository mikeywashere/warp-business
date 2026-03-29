namespace WarpBusiness.Api.Services;

/// <summary>
/// Provisions ASP.NET Identity user accounts from existing Employee records.
/// </summary>
public interface IEmployeeUserService
{
    /// <summary>
    /// Creates an ApplicationUser from an Employee, assigns the "User" role,
    /// links them to the specified tenant via UserTenant, and generates a temporary password.
    /// </summary>
    Task<CreateUserFromEmployeeResult> CreateUserFromEmployeeAsync(
        Guid employeeId, Guid tenantId, CancellationToken ct = default);
}

public record CreateUserFromEmployeeResult
{
    public bool Success { get; init; }
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public string? TemporaryPassword { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
    public string? Error { get; init; }
    public CreateUserFromEmployeeError ErrorCode { get; init; }
}

public enum CreateUserFromEmployeeError
{
    None,
    EmployeeNotFound,
    UserAlreadyExists,
    CreationFailed
}
