using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Developer.API.DTOs;

public sealed class PublicPropertySearch
{
    [MaxLength(64)] public string? LocationId { get; set; }
    [MaxLength(50)] public List<string> SelectedNearbyLocationIds { get; set; } = [];
    [MaxLength(200)] public string? Location { get; set; }
    [MaxLength(50)] public string? PropertyType { get; set; }
    [RegularExpression("^(Sale|Rent|Both|Room|All)$")] public string ListingType { get; set; } = "All";
    [Range(0, 999999999999d)] public decimal? MinPrice { get; set; }
    [Range(0, 999999999999d)] public decimal? MaxPrice { get; set; }
    [Range(-90, 90)] public double? Latitude { get; set; }
    [Range(-180, 180)] public double? Longitude { get; set; }
    [Range(1, 100)] public double RadiusKm { get; set; } = 15;
    [RegularExpression("^(All|PrivateRoom|SharedRoom)$")] public string RoomLettingType { get; set; } = "All";
    [MaxLength(50)] public List<string> SelectedNearbyAreas { get; set; } = [];
    public bool IncludeNearby { get; set; } = true;
    public bool Randomize { get; set; }
    public bool HotOnly { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 48)] public int PageSize { get; set; } = 12;
}

public sealed record PublicAmenity(string Name, string Label, bool? BooleanValue, int? NumberValue, int? UnitId);

public sealed record PublicOwnerContact(string Name, string Email, string Phone);
public sealed record PublicUnit(int Id, string Number, string Description, decimal AreaSqM,
    decimal? RentalPrice, decimal? SalePrice, bool IsVacant, IReadOnlyList<string> Images, decimal? AdvertisedDepositAmount = null, IReadOnlyList<PublicAmenity>? Amenities = null);
public sealed record PublicProperty(int Id, string Address, string Description, string PropertyType,
    string ErfSize, bool IsForSale, bool IsForRent, decimal? SalePrice, decimal? RentalPrice,
    decimal Latitude, decimal Longitude, bool IsHot, double? DistanceKm, bool IsNearby,
    IReadOnlyList<string> Images, PublicOwnerContact Owner, IReadOnlyList<PublicUnit> Units,
    string? ReviewStatus = null, string RoomLettingType = "None", string? Thumb = null, PropertyLocation? Location = null, string? GooglePlaceId = null, string? PostalCode = null, IReadOnlyList<PublicAmenity>? Amenities = null, bool IsPublicListing = false, DateTime? LettingExpiresAt = null);
public sealed record PublicPropertyMatch(int Id, double? DistanceKm, bool IsNearby);
public sealed record NearbyAreaOption(string Name, int PropertyCount, double DistanceKm, string? LocationId = null);
public sealed record PublicSearchResult(IReadOnlyList<PublicProperty> Items, int Total, int Page,
    int PageSize, bool NearbyAvailable, IReadOnlyList<NearbyAreaOption>? NearbyAreas = null);

public sealed class OwnerCredentials
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MinLength(12), MaxLength(128)] public string Password { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(40)] public string Phone { get; set; } = "";
}

public sealed class OwnerPropertySubmission
{
    public PropertyLocation? Location { get; set; }
    [MaxLength(300)] public string? GooglePlaceId { get; set; }
    [MaxLength(40)] public string? PostalCode { get; set; }
    [Required, MaxLength(500)] public string Address { get; set; } = "";
    [Required, MaxLength(10000)] public string Description { get; set; } = "";
    [Required, MaxLength(50)] public string PropertyType { get; set; } = "";
    [Required, MaxLength(100)] public string ErfSize { get; set; } = "";
    public bool IsForSale { get; set; }
    public bool IsForRent { get; set; }
    [Range(1, 999999999999d)] public decimal? SalePrice { get; set; }
    [Range(-90, 90)] public decimal Latitude { get; set; }
    [Range(-180, 180)] public decimal Longitude { get; set; }
    [MaxLength(10)] public List<string> Images { get; set; } = [];
    [MaxLength(100)] public List<OwnerUnitSubmission> Units { get; set; } = [];
    [RegularExpression("^(None|PrivateRoom|SharedRoom)$")] public string RoomLettingType { get; set; } = "None";
    public bool PublishContactConsent { get; set; }
}

public sealed class OwnerUnitSubmission
{
    [Range(0, 999999999999d)] public decimal? AdvertisedDepositAmount { get; set; }
    [Required, MaxLength(50)] public string Number { get; set; } = "";
    [Required, MaxLength(100)] public string Description { get; set; } = "";
    [Range(0, 99999999)] public decimal AreaSqM { get; set; }
    [Range(1, 999999999999d)] public decimal? RentalPrice { get; set; }
    [Range(1, 999999999999d)] public decimal? SalePrice { get; set; }
    public bool IsVacant { get; set; } = true;
    [MaxLength(10)] public List<string> Images { get; set; } = [];
}
