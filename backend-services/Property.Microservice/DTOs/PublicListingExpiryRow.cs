namespace Property.Microservice.DTOs;

/// <summary>Agency-scoped public letting term report; no private tenant or lease information.</summary>
public sealed record PublicListingExpiryRow(int PropertyId, string TenantId, string CompleteAddress,
    DateTime? PublicPublishedAt, DateTime? PublicLettingExpiresAt, bool IsPublicListing, bool IsForSale, string PublicReviewStatus);
