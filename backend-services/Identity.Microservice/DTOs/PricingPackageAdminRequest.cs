using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.DTOs;

public sealed record PricingPackageAdminRequest
{
    [Required, RegularExpression("^[A-Z][A-Z0-9_]{0,49}$")] public string Code { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [StringLength(2000)] public string? Description { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal? MonthlyPrice { get; init; }
    [Range(typeof(decimal), "0", "999999999")] public decimal? AnnualPrice { get; init; }
    [Required, MaxLength(100)] public string[] Features { get; init; } = [];
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
