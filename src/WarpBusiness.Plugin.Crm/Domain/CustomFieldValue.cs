namespace WarpBusiness.Plugin.Crm.Domain;

public class CustomFieldValue
{
    public Guid Id { get; set; }
    public Guid FieldDefinitionId { get; set; }
    public CustomFieldDefinition FieldDefinition { get; set; } = null!;
    public Guid ContactId { get; set; }
    public Contact Contact { get; set; } = null!;
    public string? Value { get; set; } // always stored as string
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
