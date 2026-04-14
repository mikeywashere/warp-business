namespace WarpBusiness.Crm.Models;

public class CustomerEmployee
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public decimal? BillingRate { get; set; }
    public string BillingCurrency { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    public Customer? Customer { get; set; }
}
