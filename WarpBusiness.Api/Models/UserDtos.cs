namespace WarpBusiness.Api.Models;

public record CreateUserRequest(string FirstName, string LastName, string Email, string Password, UserRole Role, string? Username = null, Guid? TenantId = null);
public record UpdateUserRequest(string FirstName, string LastName, string Email, UserRole Role);
public record UpdateProfileRequest(string FirstName, string LastName);
public record UserResponse(Guid Id, string FirstName, string LastName, string Email, string Username, UserRole Role, DateTime CreatedAt, Guid? LinkedEmployeeId = null);
public record UserWithTenantsResponse(Guid Id, string FirstName, string LastName, string Email, string Username, UserRole Role, DateTime CreatedAt, List<UserTenantInfo> Tenants, Guid? LinkedEmployeeId = null);
public record UserTenantInfo(Guid TenantId, string TenantName);
