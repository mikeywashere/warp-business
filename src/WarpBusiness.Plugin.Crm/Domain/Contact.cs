namespace WarpBusiness.Plugin.Crm.Domain;

public class Contact
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? Notes { get; set; }
    public ContactStatus Status { get; set; } = ContactStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<CustomFieldValue> CustomFieldValues { get; set; } = new List<CustomFieldValue>();
}

public enum ContactStatus { Active, Inactive, Lead, Customer }
