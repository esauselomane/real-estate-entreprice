using System.ComponentModel.DataAnnotations;

namespace Developer.API.DTOs;

/// <summary>Visitor details confirmed before creating a property enquiry.</summary>
public sealed class PublicEnquiryRequest : IValidatableObject
{
    public Guid RequestId { get; set; }
    [RegularExpression("^(Sale|Rent)$")] public string ListingType { get; set; } = "Rent";
    [Range(1, int.MaxValue)] public int PropertyId { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Surname { get; set; } = "";
    [EmailAddress, MaxLength(254)] public string? Email { get; set; }
    [Phone, MaxLength(40)] public string? Phone { get; set; }
    [Required, MaxLength(2000)] public string Message { get; set; } = "";
    [MaxLength(80), RegularExpression("^[a-zA-Z0-9_-]*$")] public string ReferralCode { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestId == Guid.Empty) yield return new ValidationResult("An enquiry request ID is required.", [nameof(RequestId)]);
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Phone))
            yield return new ValidationResult("Provide a name or cellphone number.", [nameof(Name), nameof(Phone)]);
    }
}
