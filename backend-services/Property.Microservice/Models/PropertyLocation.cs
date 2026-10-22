using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models;

/// <summary>A user-confirmed administrative address, independent of street addresses and country-specific labels.</summary>
public sealed class PropertyLocation
{
    [Key, MaxLength(64)] public string LocationId { get; set; } = "";
    [Required, MaxLength(2), RegularExpression("^[A-Za-z]{2}$")] public string CountryCode { get; set; } = "";
    [MaxLength(150)] public string Country { get; set; } = "";
    [MaxLength(150)] public string Region { get; set; } = "";
    [MaxLength(150)] public string District { get; set; } = "";
    [MaxLength(150)] public string City { get; set; } = "";
    [MaxLength(150)] public string Suburb { get; set; } = "";
}
