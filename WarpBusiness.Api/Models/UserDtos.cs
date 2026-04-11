namespace WarpBusiness.Api.Models;

public record CreateUserRequest(string FirstName, string LastName, string Email, string Password, UserRole Role);
public record UpdateUserRequest(string FirstName, string LastName, string Email, UserRole Role);
public record UpdateProfileRequest(string FirstName, string LastName);
public record UserResponse(Guid Id, string FirstName, string LastName, string Email, UserRole Role, DateTime CreatedAt);
