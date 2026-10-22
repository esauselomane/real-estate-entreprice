using Property.Microservice.Models;

namespace Property.Microservice.Services.PublicListings;

/// <summary>Letting terms run for two calendar months from agency publication or explicit renewal.</summary>
public static class PublicListingTerm
{
    public static bool IsLettingAvailable(PropertyAsset property, DateTime now) => property.IsForRent && (!property.PublicLettingExpiresAt.HasValue || property.PublicLettingExpiresAt > now);
    public static bool IsVisible(PropertyAsset property, DateTime now) => property.IsPublicListing && property.PublicReviewStatus == "Approved" && (property.IsForSale || IsLettingAvailable(property, now));
    public static void Publish(PropertyAsset property, DateTime now, bool renewLetting)
    {
        property.PublicPublishedAt ??= now;
        if (property.IsForRent && (!property.PublicLettingExpiresAt.HasValue || renewLetting)) property.PublicLettingExpiresAt = now.AddMonths(2);
    }
}
