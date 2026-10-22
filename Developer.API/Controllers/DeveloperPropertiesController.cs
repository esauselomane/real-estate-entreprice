using Developer.API.DTOs;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Developer.API.Controllers;

[ApiController]
[Route("api/developer/properties")]
public sealed class DeveloperPropertiesController(PropertyDbContext database) : ControllerBase
{
    private IQueryable<PropertyAsset> PublicProperties() => database.PropertyAssets.AsNoTracking()
        .Where(property => property.TenantId == (string)HttpContext.Items["AgencyId"]! && property.IsPublicListing && property.PublicReviewStatus == "Approved" && (property.IsForSale || (property.IsForRent && (!property.PublicLettingExpiresAt.HasValue || property.PublicLettingExpiresAt > DateTime.UtcNow))))
        .Include(property => property.PropertyTypeDetails).Include(property => property.SubUnits);

    [HttpGet("locations")]
    public async Task<IActionResult> Locations([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var locations = PublicProperties().Where(item => item.LocationId != null).Select(item => item.Location!);
        if (!string.IsNullOrWhiteSpace(q))
            locations = locations.Where(item => item.Suburb.Contains(q) || item.City.Contains(q) || item.Region.Contains(q) || item.Country.Contains(q));
        Response.Headers.CacheControl = "no-store";
        return Ok(await locations.Distinct().OrderBy(item => item.Suburb).ThenBy(item => item.City).Take(30).ToArrayAsync(cancellationToken));
    }

    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken cancellationToken) =>
        Ok(await database.PropertyTypes.AsNoTracking().OrderBy(type => type.Name).Select(type => type.Name).ToArrayAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var property = await PublicProperties().FirstOrDefaultAsync(item => item.PropertyId == id, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        if (property is null) return NotFound();
        var unitIds = property.SubUnits.Select(unit => unit.PropertyUnitId).ToArray();
        var amenities = await database.PropertyUnitAmenities.AsNoTracking()
            .Where(item => item.PropertyId == id && item.TenantId == property.TenantId && item.IsActive && item.AmenityType != null && item.AmenityType.IsActive)
            .OrderBy(item => item.AmenityType!.Name)
            .Select(item => new PublicAmenity(item.AmenityType!.Name, item.AmenityType.Label ?? item.AmenityType.Name, item.BooleanValue, item.NumberValue, item.PropertyUnitId))
            .ToArrayAsync(cancellationToken);
        var visibleAmenities = amenities.Where(item => item.UnitId == null || unitIds.Contains(item.UnitId.Value)).ToArray();
        return Ok(PublicListingMapper.Map(property, amenities: visibleAmenities));
    }

    [HttpGet("hot")]
    public Task<IActionResult> Hot([FromQuery] PublicPropertySearch search, CancellationToken cancellationToken)
    {
        search.HotOnly = true;
        return Search(search, cancellationToken);
    }

    /// <summary>Matches the requested area first, then adds properties within the configured great-circle radius.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] PublicPropertySearch search, CancellationToken cancellationToken)
    {
        if (search.Latitude.HasValue != search.Longitude.HasValue) return BadRequest(new { message = "Supply both latitude and longitude." });
        if (search.MinPrice > search.MaxPrice) return BadRequest(new { message = "Minimum price must not exceed maximum price." });
        if (string.IsNullOrWhiteSpace(search.LocationId) && !string.IsNullOrWhiteSpace(search.Location))
        {
            var text = search.Location.Trim().ToLowerInvariant();
            var distinctLocations = await PublicProperties().Where(item => item.Location != null && (item.Location.Suburb.ToLower() == text || item.Location.City.ToLower() == text))
                .Select(item => item.LocationId).Distinct().Take(2).CountAsync(cancellationToken);
            if (distinctLocations > 1) return Conflict(new { message = "Several places have this name. Select a location including its city, region and country from the suggestions." });
        }
        var query = ApplyFilters(PublicProperties(), search);
        double? originLatitude = search.Latitude;
        double? originLongitude = search.Longitude;
        if (!string.IsNullOrWhiteSpace(search.LocationId))
        {
            var anchor = await PublicProperties().Where(item => item.LocationId == search.LocationId)
                .OrderBy(item => item.PropertyId).Select(item => new { item.Latitude, item.Longitude }).FirstOrDefaultAsync(cancellationToken);
            if (anchor is not null) { originLatitude = (double)anchor.Latitude; originLongitude = (double)anchor.Longitude; }
        }
        else if (!originLatitude.HasValue && !string.IsNullOrWhiteSpace(search.Location))
        {
            // Use a known listing in this agency as the area anchor; never invent coordinates.
            var normalizedLocation = search.Location.ToLowerInvariant();
            var anchor = await PublicProperties().Where(item => item.CompleteAddress.ToLower().Contains(normalizedLocation))
                .OrderBy(item => item.PropertyId).Select(item => new { item.Latitude, item.Longitude }).FirstOrDefaultAsync(cancellationToken);
            if (anchor is not null) { originLatitude = (double)anchor.Latitude; originLongitude = (double)anchor.Longitude; }
        }
        // Read only coordinates and IDs before paging. Gallery blobs and units are loaded for the requested page only.
        var candidates = await query.Select(item => new { item.PropertyId, item.CompleteAddress, item.Latitude, item.Longitude, item.LocationId, item.Location })
            .ToListAsync(cancellationToken);
        if (search.SelectedNearbyAreas.Any(area => string.IsNullOrWhiteSpace(area) || area.Length > 200))
            return BadRequest(new { message = "Nearby area names must contain 1 to 200 characters." });
        var selectedAreas = search.SelectedNearbyAreas.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var areaOptions = new Dictionary<string, NearbyAreaOption>(StringComparer.OrdinalIgnoreCase);
        var matches = new List<PublicPropertyMatch>();
        foreach (var property in candidates)
        {
            var hasLocation = !string.IsNullOrWhiteSpace(search.LocationId) || !string.IsNullOrWhiteSpace(search.Location);
            var exactLocation = !hasLocation || (!string.IsNullOrWhiteSpace(search.LocationId)
                ? property.LocationId == search.LocationId
                : property.CompleteAddress.Contains(search.Location!, StringComparison.OrdinalIgnoreCase));
            double? distance = null;
            if (originLatitude.HasValue && originLongitude.HasValue)
                distance = PublicListingMapper.DistanceKm(originLatitude.Value, originLongitude.Value, (double)property.Latitude, (double)property.Longitude);
            if (!exactLocation)
            {
                if (distance is null || distance > search.RadiusKm) continue;
                var areaName = property.Location is null ? GetAreaName(property.CompleteAddress) : Property.Microservice.Services.Locations.PropertyLocationResolver.Label(property.Location);
                AddAreaOption(areaOptions, areaName, distance.Value, property.LocationId);
                if (!search.IncludeNearby) continue;
                if (search.SelectedNearbyLocationIds.Count > 0)
                {
                    var selectedById = property.LocationId is not null && search.SelectedNearbyLocationIds.Contains(property.LocationId);
                    var selectedLegacyArea = property.LocationId is null && selectedAreas.Contains(areaName);
                    if (!selectedById && !selectedLegacyArea) continue;
                }
                else if (selectedAreas.Count > 0 && !selectedAreas.Contains(areaName)) continue;
            }
            if (!hasLocation && distance > search.RadiusKm) continue;
            matches.Add(new PublicPropertyMatch(property.PropertyId, distance, !exactLocation));
        }
        var ordered = OrderMatches(matches, search);
        var page = ordered.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToArray();
        var propertyIds = page.Select(item => item.Id).ToArray();
        var properties = await PublicProperties().Where(item => propertyIds.Contains(item.PropertyId))
            .ToDictionaryAsync(item => item.PropertyId, cancellationToken);
        var results = new List<PublicProperty>();
        foreach (var match in page)
        {
            // Recheck publication in the second query so withdrawn records cannot slip through pagination.
            if (!properties.TryGetValue(match.Id, out var property)) continue;
            var distance = match.DistanceKm.HasValue ? Math.Round(match.DistanceKm.Value, 1) : (double?)null;
            results.Add(PublicListingMapper.Map(property, distance, match.IsNearby));
        }
        Response.Headers.CacheControl = "no-store";
        return Ok(new PublicSearchResult(results, matches.Count, search.Page, search.PageSize, originLatitude.HasValue, areaOptions.Values.OrderBy(area => area.DistanceKm).ThenBy(area => area.Name).ToArray()));
    }

    private static string GetAreaName(string address)
    {
        var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2) return parts[^2] + ", " + parts[^1];
        return parts.LastOrDefault() ?? address;
    }

    private static void AddAreaOption(Dictionary<string, NearbyAreaOption> options, string name, double distance, string? locationId)
    {
        if (options.TryGetValue(name, out var existing))
            options[name] = existing with { PropertyCount = existing.PropertyCount + 1, DistanceKm = Math.Min(existing.DistanceKm, Math.Round(distance, 1)) };
        else options[name] = new NearbyAreaOption(name, 1, Math.Round(distance, 1), locationId);
    }

    private static IEnumerable<PublicPropertyMatch> OrderMatches(List<PublicPropertyMatch> matches, PublicPropertySearch search)
    {
        if (search.HotOnly && search.Randomize)
        {
            var randomMatches = matches.ToArray();
            Random.Shared.Shuffle(randomMatches);
            return randomMatches;
        }
        return matches.OrderBy(item => item.IsNearby).ThenBy(item => item.DistanceKm).ThenByDescending(item => item.Id);
    }

    private static IQueryable<PropertyAsset> ApplyFilters(IQueryable<PropertyAsset> query, PublicPropertySearch search)
    {
        if (search.HotOnly) query = query.Where(item => item.IsHotProperty);
        if (!string.IsNullOrWhiteSpace(search.PropertyType) && search.PropertyType != "All")
            query = query.Where(item => item.PropertyTypeDetails != null && item.PropertyTypeDetails.Name == search.PropertyType);
        if (search.ListingType == "Sale") query = query.Where(item => item.IsForSale);
        if (search.ListingType == "Rent") query = query.Where(item => item.IsForRent && (!item.PublicLettingExpiresAt.HasValue || item.PublicLettingExpiresAt > DateTime.UtcNow));
        if (search.ListingType == "Both") query = query.Where(item => item.IsForSale && item.IsForRent && (!item.PublicLettingExpiresAt.HasValue || item.PublicLettingExpiresAt > DateTime.UtcNow));
        if (search.ListingType == "Room") query = query.Where(item => item.IsForRent && (!item.PublicLettingExpiresAt.HasValue || item.PublicLettingExpiresAt > DateTime.UtcNow) && (item.RoomLettingType != "None" || item.PropertyTypeDetails!.Name == "Room share/let"));
        if (search.RoomLettingType != "All") query = query.Where(item => item.RoomLettingType == search.RoomLettingType);
        if (search.ListingType is "Rent" or "Room")
        {
            query = FilterRentalPrice(query, search);
        }
        else
        {
            if (search.MinPrice.HasValue) query = query.Where(item => item.SalePrice >= search.MinPrice);
            if (search.MaxPrice.HasValue) query = query.Where(item => item.SalePrice <= search.MaxPrice);
        }
        return query;
    }
    private static IQueryable<PropertyAsset> FilterRentalPrice(IQueryable<PropertyAsset> query, PublicPropertySearch search)
    {
        if (search.MinPrice.HasValue && search.MaxPrice.HasValue)
            return query.Where(item => item.SubUnits.Any(unit => (unit.SpecificUnitRentalPrice ?? unit.RentalRate) >= search.MinPrice && (unit.SpecificUnitRentalPrice ?? unit.RentalRate) <= search.MaxPrice));
        if (search.MinPrice.HasValue)
            return query.Where(item => item.SubUnits.Any(unit => (unit.SpecificUnitRentalPrice ?? unit.RentalRate) >= search.MinPrice));
        if (search.MaxPrice.HasValue)
            return query.Where(item => item.SubUnits.Any(unit => (unit.SpecificUnitRentalPrice ?? unit.RentalRate) <= search.MaxPrice));
        return query;
    }

}
