using System.ComponentModel.DataAnnotations;
namespace Identity.Microservice.DTOs;
public sealed record CreateAgencyTenantRequest
{
    [Required] public string TenantId { get; init; } = string.Empty;
    [Required] public string AgencyName { get; init; } = string.Empty;
    [Required, EmailAddress] public string ContactEmail { get; init; } = string.Empty;
    public string? ParentTenantId { get; init; }
}
