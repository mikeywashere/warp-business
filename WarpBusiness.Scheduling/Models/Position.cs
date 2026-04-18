namespace WarpBusiness.Scheduling.Models;

public class Position
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Hex color code (e.g. "#3B82F6") for schedule grid UI.</summary>
    public string Color { get; set; } = "#6B7280";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
