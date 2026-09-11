namespace CRM.Microservice.DTOs;

/// <summary>
/// Represents a single monthly target entry for a broker, including
/// the target amount, the real commission achieved, and progress percentage.
/// </summary>
public record BrokerMonthlyTargetDto
{
    public int Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string BrokerId { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetCommission { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request payload for setting or updating a broker's monthly target.
/// </summary>
public record SetBrokerMonthlyTargetRequest
{
    public string? BrokerId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetCommission { get; init; }
}

/// <summary>
/// Combines the broker's monthly target with the realised commission
/// for the same period, so the gauge chart can compute progress.
/// </summary>
public record BrokerTargetWithProgressDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetCommission { get; init; }
    public decimal RealisedCommission { get; init; }
    public decimal ProgressPercentage { get; init; }
}
