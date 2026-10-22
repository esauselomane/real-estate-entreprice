using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.DTOs;

namespace Property.Microservice.Controllers;

public partial class PropertyAssetsController
{
    /// <summary>Agency approval is the only path that can publish an owner submission.</summary>
    [HttpPut("{propertyId:int}/public-listing")]
    public async Task<IActionResult> UpdatePublicListing(int propertyId, PublicListingSettingsRequest request, CancellationToken cancellationToken)
    {
        if (IsReadOnlyRole() || !await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(PropertyRepository.Query())
            .FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);
        if (property is null) return NotFound();
        var canManageHotStatus = Property.Microservice.Services.PublicListings.HotPropertyAuthorization.CanManage(
            GetCurrentCanonicalRole(), User.FindFirst("TenantId")?.Value, property.TenantId);
        if (request.IsHotProperty.HasValue && request.IsHotProperty.Value != property.IsHotProperty && !canManageHotStatus) return Forbid();
        property.RoomLettingType = request.RoomLettingType;
        if (request.RoomLettingType != "None")
        {
            var roomType = await DbContext.PropertyTypes.SingleAsync(item => item.Name == "Room share/let", cancellationToken);
            property.PropertyTypeId = roomType.PropertyTypeId;
        }

        property.PublicOwnerName = request.OwnerName.Trim();
        property.PublicOwnerEmail = request.OwnerEmail.Trim();
        property.PublicOwnerPhone = request.OwnerPhone.Trim();
        if (request.IsPublicListing && !property.IsForSale && property.IsForRent && property.PublicLettingExpiresAt <= DateTime.UtcNow && !request.RenewLetting)
            return BadRequest(new { message = "The letting term has expired. Explicitly renew it before publishing." });
        if (request.IsPublicListing) Property.Microservice.Services.PublicListings.PublicListingTerm.Publish(property, DateTime.UtcNow, request.RenewLetting);
        property.IsPublicListing = request.IsPublicListing;
        if (canManageHotStatus && request.IsHotProperty.HasValue) property.IsHotProperty = request.IsPublicListing && request.IsHotProperty.Value;
        property.PublicReviewStatus = request.IsPublicListing ? "Approved" : "Unpublished";
        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { property.IsPublicListing, property.IsHotProperty, property.PublicReviewStatus, property.PublicLettingExpiresAt });
    }
    /// <summary>Reports published letting terms within an agency-visible UTC date range, including mixed sale/letting listings.</summary>
    [HttpGet("public-listings/expiring")]
    public async Task<IActionResult> ExpiringListings([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:view")) return Forbid();
        var start = fromUtc ?? DateTime.UtcNow;
        var end = toUtc ?? start.AddDays(30);
        if (end < start || end > start.AddYears(1)) return BadRequest(new { message = "Choose a date range of up to one year." });
        var rows = await ApplyPropertyVisibility(PropertyRepository.Query()).AsNoTracking()
            .Where(property => property.IsForRent && property.PublicLettingExpiresAt >= start && property.PublicLettingExpiresAt <= end)
            .OrderBy(property => property.PublicLettingExpiresAt)
            .Select(property => new PublicListingExpiryRow(property.PropertyId, property.TenantId, property.CompleteAddress, property.PublicPublishedAt, property.PublicLettingExpiresAt, property.IsPublicListing, property.IsForSale, property.PublicReviewStatus))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }
}
