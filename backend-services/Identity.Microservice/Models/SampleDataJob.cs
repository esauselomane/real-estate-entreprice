using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.Models;

public sealed class SampleDataJob
{
    [Key, MaxLength(64)] public string TenantId { get; set; } = string.Empty;
    public string State { get; set; } = "Requested";
    public string PlanJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ProvisionedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
}
