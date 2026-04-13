namespace WarpBusiness.Employees.Services;

/// <summary>
/// Validates user references when linking employees to users.
/// Implemented in the API layer which has access to user data.
/// </summary>
public interface IUserValidator
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UserBelongsToTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
