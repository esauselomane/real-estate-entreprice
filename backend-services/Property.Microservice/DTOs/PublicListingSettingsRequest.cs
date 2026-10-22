using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.DTOs;

public sealed record PublicListingSettingsRequest(
    bool IsPublicListing,
    bool? IsHotProperty,
    [Required, MaxLength(200)] string OwnerName,
    [Required, EmailAddress, MaxLength(254)] string OwnerEmail,
    [Required, Phone, MaxLength(40)] string OwnerPhone,
    [RegularExpression("^(None|PrivateRoom|SharedRoom)$")] string RoomLettingType = "None",
    bool RenewLetting = false);
