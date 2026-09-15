using System.ComponentModel.DataAnnotations;

namespace CRM.Microservice.DTOs;

/// <summary>Agency details supplied by an administrator; identifiers and tenant scope are assigned by the server.</summary>
public sealed class CreateAgencyRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string ContactEmail { get; set; } = string.Empty;
    public string TradingName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string CompanyLogoUrl { get; set; } = string.Empty;
    public string HeadquartersAddress { get; set; } = string.Empty;
    public string BrokerageLicense { get; set; } = string.Empty;
    public string? ParentAgencyId { get; set; }
    public bool IsTrial { get; set; }
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
}
