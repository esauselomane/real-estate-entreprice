namespace Property.Microservice.Models.Syndication;

public sealed class SyndicationCommand
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public int Revision { get; set; }
    public string Operation { get; set; } = "Publish";
    public string SnapshotJson { get; set; } = string.Empty;
    public string RequestKey { get; set; } = Guid.NewGuid().ToString("N");
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}
