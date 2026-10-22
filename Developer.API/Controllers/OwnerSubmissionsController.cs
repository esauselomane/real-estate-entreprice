using System.Text.Json;
using Developer.API.DTOs;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Developer.API.Controllers;

[ApiController]
[Route("api/developer/submissions")]
[RequestSizeLimit(30_000_000)]
public sealed class OwnerSubmissionsController(PropertyDbContext database, OwnerSessionService sessions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var owner = await sessions.FindOwnerAsync(HttpContext, cancellationToken);
        if (owner is null) return Unauthorized();
        var properties = await OwnedProperties(owner.Id, owner.AgencyId).AsNoTracking()
            .OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<PublicProperty>();
        foreach (var property in properties) result.Add(PublicListingMapper.Map(property, includeReview: true));
        Response.Headers.CacheControl = "no-store";
        return Ok(result);
    }

    [HttpPost]
    public Task<IActionResult> Create(OwnerPropertySubmission request, CancellationToken cancellationToken) => Save(null, request, cancellationToken);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, OwnerPropertySubmission request, CancellationToken cancellationToken) => Save(id, request, cancellationToken);

    /// <summary>Owners may withdraw only their own agency-scoped listing; publication remains an agency action.</summary>
    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        var owner = await sessions.FindOwnerAsync(HttpContext, cancellationToken);
        if (owner is null) return Unauthorized();
        var property = await OwnedProperties(owner.Id, owner.AgencyId).FirstOrDefaultAsync(item => item.PropertyId == id, cancellationToken);
        if (property is null) return NotFound();
        property.IsPublicListing = false;
        property.PublicReviewStatus = "Unpublished";
        // Keep agency inventory, units and the administrator-controlled hot designation intact.
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { property.PropertyId, property.IsPublicListing, property.PublicReviewStatus });
    }

    /// <summary>All owner changes return the listing to review and withdraw public visibility.</summary>
    private async Task<IActionResult> Save(int? id, OwnerPropertySubmission request, CancellationToken cancellationToken)
    {
        var owner = await sessions.FindOwnerAsync(HttpContext, cancellationToken);
        if (owner is null) return Unauthorized();
        var error = ValidateSubmission(request);
        if (error is not null) return BadRequest(new { message = error });
        var propertyType = await database.PropertyTypes.FirstOrDefaultAsync(item => item.Name == (request.RoomLettingType != "None" ? "Room share/let" : request.PropertyType), cancellationToken);
        if (propertyType is null) return BadRequest(new { message = "Choose a supported property type." });
        PropertyAsset property;
        if (id.HasValue)
        {
            var existing = await OwnedProperties(owner.Id, owner.AgencyId).FirstOrDefaultAsync(item => item.PropertyId == id.Value, cancellationToken);
            if (existing is null) return NotFound();
            property = existing;
            // Preserve unit IDs used by operational records; owners cannot remove existing units.
            if (request.Units.Count < property.SubUnits.Count) return BadRequest(new { message = "Existing units cannot be removed. Mark unavailable units as occupied." });
        }
        else
        {
            property = new PropertyAsset { TenantId = owner.AgencyId, PublicOwnerAccountId = owner.Id, LinkedOwnerId = "public:" + owner.Id, TitleDeedType = "Owner submission", PortfolioZoning = request.PropertyType };
            database.PropertyAssets.Add(property);
        }
        var existingImages = PublicListingMapper.ReadImages(property.GalleryImageJson).Concat(property.SubUnits.SelectMany(ReadUnitImages)).ToHashSet(StringComparer.Ordinal);
        try
        {
            request.Images = ProcessImages(request.Images, existingImages);
            foreach (var unit in request.Units) unit.Images = ProcessImages(unit.Images, existingImages);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        var addressChanged = property.CompleteAddress != request.Address.Trim() || property.Latitude != request.Latitude || property.Longitude != request.Longitude;
        if (request.Location is not null || addressChanged)
        {
            property.Location = await Property.Microservice.Services.Locations.PropertyLocationResolver.ResolveAsync(database, request.Location, cancellationToken);
            property.LocationId = property.Location?.LocationId;
            property.GooglePlaceId = request.GooglePlaceId;
            property.PostalCode = request.PostalCode;
        }
        property.CompleteAddress = request.Address.Trim();
        property.PropertyDescription = request.Description.Trim();
        property.PropertyTypeId = propertyType.PropertyTypeId;
        property.ErfSpatialSize = request.ErfSize.Trim();
        property.IsForSale = request.IsForSale;
        property.RoomLettingType = request.RoomLettingType;
        property.IsForRent = request.IsForRent;
        property.MarketClass = request.IsForSale ? "Sale" : "Rental";
        property.SalePrice = request.SalePrice;
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.GalleryImageJson = JsonSerializer.Serialize(request.Images);
        property.PublicOwnerName = owner.Name;
        property.PublicOwnerEmail = owner.Email;
        property.PublicOwnerPhone = owner.Phone;
        property.IsPublicListing = false;
        // Owner edits withdraw publication without changing the agency administrator's hot designation.
        property.PublicReviewStatus = "Pending review";
        property.PublicSubmittedAt = DateTime.UtcNow;
        ApplyUnits(property, request.Units);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { property.PropertyId, status = property.PublicReviewStatus });
    }

    private IQueryable<PropertyAsset> OwnedProperties(string ownerId, string agencyId) => database.PropertyAssets
        .Where(item => item.TenantId == agencyId && item.PublicOwnerAccountId == ownerId)
        .Include(item => item.SubUnits).Include(item => item.PropertyTypeDetails);

    private static string? ValidateSubmission(OwnerPropertySubmission request)
    {
        if (!request.PublishContactConsent) return "Confirm that your name and contact details may be published.";
        if (!request.IsForSale && !request.IsForRent) return "Select for sale, to let, or both.";
        if (request.PropertyType == "Room share/let" && request.RoomLettingType == "None") return "Choose a private or shared room.";
        if (request.IsForSale && request.SalePrice is null) return "A sale price is required.";
        if (request.IsForRent && !request.Units.Any(unit => unit.RentalPrice.HasValue)) return "At least one unit with a monthly rental price is required.";
        if (request.Units.Select(unit => unit.Number.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Units.Count) return "Unit numbers must be unique.";
        if (request.Images.Count + request.Units.Sum(unit => unit.Images.Count) > Property.Microservice.Services.Images.PropertyImageProcessor.MaximumPictures)
            return "A property can have at most 10 photographs, including unit photographs.";
        return null;
    }

    private static IReadOnlyList<string> ReadUnitImages(PropertyUnit unit) => PublicListingMapper.ReadImages(unit.UnitImagesJson);

    private static List<string> ProcessImages(IEnumerable<string> sources, HashSet<string> existingImages)
    {
        var images = new List<string>();
        foreach (var source in sources)
        {
            if (existingImages.Contains(source)) images.Add(source);
            else images.Add(Property.Microservice.Services.Images.PropertyImageProcessor.Process(source));
        }
        return images;
    }

    private static void ApplyUnits(PropertyAsset property, IReadOnlyList<OwnerUnitSubmission> requests)
    {
        var existingUnits = property.SubUnits.OrderBy(unit => unit.PropertyUnitId).ToArray();
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var unit = index < existingUnits.Length ? existingUnits[index] : new PropertyUnit();
            unit.UnitNumber = request.Number.Trim();
            unit.UnitDescription = request.Description.Trim();
            unit.NetRentableAreaSqM = request.AreaSqM;
            unit.RentalRate = request.RentalPrice;
            unit.AdvertisedDepositAmount = request.AdvertisedDepositAmount;
            unit.SpecificUnitRentalPrice = request.RentalPrice;
            unit.SpecificUnitSalePrice = request.SalePrice;
            unit.IsCurrentlyVacant = request.IsVacant;
            unit.UnitImagesJson = JsonSerializer.Serialize(request.Images);
            if (index >= existingUnits.Length) property.SubUnits.Add(unit);
        }
    }
}
