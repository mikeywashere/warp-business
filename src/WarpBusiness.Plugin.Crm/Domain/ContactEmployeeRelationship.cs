namespace WarpBusiness.Plugin.Crm.Domain;

public class ContactEmployeeRelationship
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public Contact Contact { get; set; } = null!;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public Guid RelationshipTypeId { get; set; }
    public ContactEmployeeRelationshipType RelationshipType { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
