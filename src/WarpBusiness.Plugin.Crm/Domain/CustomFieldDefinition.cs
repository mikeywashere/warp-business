namespace WarpBusiness.Plugin.Crm.Domain;

public class CustomFieldDefinition
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Contact"; // expandable: "Company", "Deal"
    public string FieldType { get; set; } = "Text"; // "Text","Number","Date","Boolean","Select"
    public string? SelectOptions { get; set; } // JSON array, only for FieldType=Select
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<CustomFieldValue> Values { get; set; } = new List<CustomFieldValue>();
}
