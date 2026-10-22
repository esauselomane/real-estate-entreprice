namespace Property.Microservice.Services.PublicListings;

/// <summary>Hot status belongs to administrators of the property's own agency, independent of tenant switchers.</summary>
public static class HotPropertyAuthorization
{
    public static bool CanManage(string? canonicalRole, string? authenticatedAgencyId, string propertyAgencyId) =>
        canonicalRole is "AgencyAdmin" or "PlatformAdmin" && !string.IsNullOrWhiteSpace(authenticatedAgencyId) && string.Equals(authenticatedAgencyId, propertyAgencyId, StringComparison.Ordinal);
}
