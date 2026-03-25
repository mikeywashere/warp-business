namespace WarpBusiness.Api.Domain;

public class Deal
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public DealStage Stage { get; set; } = DealStage.Prospecting;
    public int Probability { get; set; } // 0-100
    public DateTimeOffset? ExpectedCloseDate { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string OwnerId { get; set; } = string.Empty; // ApplicationUser.Id
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public enum DealStage
{
    Prospecting,
    Qualification,
    Proposal,
    Negotiation,
    ClosedWon,
    ClosedLost
}
