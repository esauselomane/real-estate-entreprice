namespace Property.Microservice.Models.Syndication;

public sealed class SyndicationPublication
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ConnectionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public bool DesiredPublished { get; set; }
    public bool ConfirmedPublished { get; set; }
    public int Revision { get; set; }
    public string State { get; set; } = "NotPublished";
    public string ExternalId { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConfirmedAt { get; set; }
}
