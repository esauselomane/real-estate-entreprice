using System.ComponentModel.DataAnnotations;

namespace Shared.Access.SampleData;

/// <summary>Immutable result of a committed sample-data transaction, used for retry deduplication.</summary>
public sealed class SampleDataReceipt
{
    [Key, MaxLength(64)] public string TenantId { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
}
