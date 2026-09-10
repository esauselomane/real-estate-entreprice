namespace CRM.Microservice.Models;

public class BrokerMonthlyTarget
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string BrokerId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TargetCommission { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
