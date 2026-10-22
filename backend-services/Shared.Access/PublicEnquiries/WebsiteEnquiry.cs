using System.ComponentModel.DataAnnotations;

namespace Shared.Access.PublicEnquiries;

/// <summary>An agency-scoped, confirmed website enquiry passed between trusted services.</summary>
public sealed class WebsiteEnquiry : IValidatableObject
{
    public Guid RequestId { get; set; }
    [Required, MaxLength(100)] public string AgencyId { get; set; } = "";
    [Range(1, int.MaxValue)] public int PropertyId { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Surname { get; set; } = "";
    [EmailAddress, MaxLength(254)] public string? Email { get; set; }
    [Phone, MaxLength(40)] public string? Phone { get; set; }
    [Required, MaxLength(2000)] public string Message { get; set; } = "";
    [MaxLength(80), RegularExpression("^[a-zA-Z0-9_-]*$")] public string ReferralCode { get; set; } = "";
    [Required, MaxLength(500)] public string PropertyAddress { get; set; } = "";
    [Required, MaxLength(100)] public string PropertyType { get; set; } = "";
    [RegularExpression("^(ForSale|ForRent)$")] public string LeadType { get; set; } = "ForRent";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestId == Guid.Empty)
            yield return new ValidationResult("An enquiry request ID is required.", [nameof(RequestId)]);
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Phone))
            yield return new ValidationResult("Provide a name or cellphone number.", [nameof(Name), nameof(Phone)]);
    }
}
