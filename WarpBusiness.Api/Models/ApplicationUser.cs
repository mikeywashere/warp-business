namespace WarpBusiness.Api.Models;

public class ApplicationUser
{
    public Guid Id { get; set; }
    public string KeycloakSubjectId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserTenantMembership> TenantMemberships { get; set; } = [];
}
