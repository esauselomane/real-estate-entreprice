using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.Models;

public sealed class BillingNotification
{
    public int Id { get; set; }
    [MaxLength(200)] public string EventKey { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}
