using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models.Syndication;

public sealed class SyndicationConnection
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string Environment { get; set; } = "Sandbox";
    public string BranchId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    [JsonIgnore]
    public string ProtectedApiKey { get; set; } = string.Empty;
    [NotMapped]
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ProtectedApiKey);
    public bool Enabled { get; set; }
}
