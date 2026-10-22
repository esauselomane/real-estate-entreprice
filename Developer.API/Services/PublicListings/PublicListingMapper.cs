using System.Text.Json;
using Developer.API.DTOs;
using Property.Microservice.Models;

namespace Developer.API.Services.PublicListings;

public static class PublicListingMapper
{
    public static PublicProperty Map(PropertyAsset property, double? distance = null, bool nearby = false, bool includeReview = false, IReadOnlyList<PublicAmenity>? amenities = null)
    {
        amenities ??= [];
        var lettingAvailable = Property.Microservice.Services.PublicListings.PublicListingTerm.IsLettingAvailable(property, DateTime.UtcNow);
        var includeLettingDetails = includeReview || lettingAvailable || !property.IsForRent;
        var reviewStatus = property.PublicReviewStatus;
        if (!lettingAvailable && property.IsForRent && !property.IsForSale && property.PublicLettingExpiresAt.HasValue && property.PublicReviewStatus == "Approved") reviewStatus = "Expired";
        var images = ReadImages(property.GalleryImageJson);
        var units = property.SubUnits.OrderBy(unit => unit.PropertyUnitId).Select(unit => MapUnit(unit, amenities, includeLettingDetails)).ToArray();
        return new PublicProperty(property.PropertyId, property.CompleteAddress, property.PropertyDescription,
            property.PropertyTypeDetails?.Name ?? "", property.ErfSpatialSize, property.IsForSale,
            (includeReview ? property.IsForRent : lettingAvailable), property.SalePrice, units.Min(unit => unit.RentalPrice), property.Latitude,
            property.Longitude, property.IsHotProperty, distance, nearby, images,
            new PublicOwnerContact(property.PublicOwnerName, property.PublicOwnerEmail, property.PublicOwnerPhone),
            units, includeReview ? reviewStatus : null, property.RoomLettingType, PublicListingThumbnail.Create(images), property.Location, property.GooglePlaceId, property.PostalCode, amenities.Where(item => item.UnitId == null).ToArray(), Property.Microservice.Services.PublicListings.PublicListingTerm.IsVisible(property, DateTime.UtcNow), property.PublicLettingExpiresAt);
    }

    private static PublicUnit MapUnit(PropertyUnit unit, IReadOnlyList<PublicAmenity> amenities, bool includeLetting) => new(unit.PropertyUnitId, unit.UnitNumber,
        unit.UnitDescription, unit.NetRentableAreaSqM, includeLetting ? unit.SpecificUnitRentalPrice ?? unit.RentalRate : null,
        unit.SpecificUnitSalePrice, unit.IsCurrentlyVacant, ReadImages(unit.UnitImagesJson), includeLetting ? unit.AdvertisedDepositAmount : null, amenities.Where(item => item.UnitId == unit.PropertyUnitId).ToArray());

    public static IReadOnlyList<string> ReadImages(string json)
    {
        try { return (JsonSerializer.Deserialize<List<string>>(json) ?? []).Where(IsSafeImage).ToArray(); }
        catch (JsonException) { return []; }
    }

    public static bool IsSafeImage(string value)
    {
        if (value.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal) ||
            value.StartsWith("data:image/png;base64,", StringComparison.Ordinal) ||
            value.StartsWith("data:image/webp;base64,", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>Great-circle distance in kilometres; clamps rounding error at the antipodes.</summary>
    public static double DistanceKm(double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        const double radians = Math.PI / 180;
        var latitudeDelta = (otherLatitude - latitude) * radians;
        var longitudeDelta = (otherLongitude - longitude) * radians;
        var value = Math.Pow(Math.Sin(latitudeDelta / 2), 2) + Math.Cos(latitude * radians) *
            Math.Cos(otherLatitude * radians) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return 6371.0088 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(value, 0, 1)));
    }
}
