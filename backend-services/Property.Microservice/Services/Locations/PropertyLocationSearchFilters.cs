using Property.Microservice.DTOs;
using Property.Microservice.Models;

namespace Property.Microservice.Services.Locations;

/// <summary>Combines exact selected locations and legacy text without creating empty SQL collection expressions.</summary>
public static class PropertyLocationSearchFilters
{
    public static IQueryable<PropertyAsset> Apply(IQueryable<PropertyAsset> properties, PropertySearchCriteria criteria)
    {
        var locationIds = criteria.LocationIds;
        var textLocations = criteria.Locations;
        if (locationIds.Count > 0 && textLocations.Count > 0)
            return properties.Where(property => (property.LocationId != null && locationIds.Contains(property.LocationId)) || textLocations.Any(text => property.CompleteAddress.Contains(text)));
        if (locationIds.Count > 0)
            return properties.Where(property => property.LocationId != null && locationIds.Contains(property.LocationId));
        if (textLocations.Count > 0)
            return properties.Where(property => textLocations.Any(text => property.CompleteAddress.Contains(text)));
        return properties;
    }
}
