using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services;
using Property.Microservice.Services.BackgroundJobs;
using System.Security.Claims;
using System.Text.Json;

namespace Property.Microservice.Controllers;

/// <summary>
/// Handles property-level operations: platform management, property CRUD,
/// search, image upload, syndication, brochure generation, amenities,
/// active listings, and property documents.
/// </summary>
    [ApiController]
    [Authorize]
    [Route("api/properties")]
public partial class PropertyAssetsController : PropertyControllerBase
{
    private readonly IRepository<ExternalPlatform> _externalPlatformRepository;
    private readonly IRepository<SoleMandate> _soleMandateRepository;
    private readonly IRepository<PropertyPlatformSyndication> _propertyPlatformSyndicationRepository;
    private readonly IRepository<AmenityType> _amenityTypeRepository;
    private readonly IRepository<PropertyUnitAmenity> _propertyUnitAmenityRepository;
    private readonly IDocumentEncryptionService _documentEncryptionService;
    private readonly IConfiguration _configuration;

    public PropertyAssetsController(
        PropertyDbContext dbContext,
        PermissionCheckService permissionCheck,
        IPropertyRepository propertyRepository,
        IPropertyUnitRepository propertyUnitRepository,
        ILogger<PropertyAssetsController> logger,
        IRepository<ExternalPlatform> externalPlatformRepository,
        IRepository<SoleMandate> soleMandateRepository,
        IRepository<PropertyPlatformSyndication> propertyPlatformSyndicationRepository,
        IRepository<AmenityType> amenityTypeRepository,
        IRepository<PropertyUnitAmenity> propertyUnitAmenityRepository,
        IJobQueue<ImageProcessingJob> imageProcessingQueue,
        IJobQueue<SyndicationJob> syndicationQueue,
        IDocumentEncryptionService documentEncryptionService,
        IConfiguration configuration)
        : base(dbContext, permissionCheck, propertyRepository, propertyUnitRepository, logger, imageProcessingQueue, syndicationQueue)
    {
        _externalPlatformRepository = externalPlatformRepository;
        _soleMandateRepository = soleMandateRepository;
        _propertyPlatformSyndicationRepository = propertyPlatformSyndicationRepository;
        _amenityTypeRepository = amenityTypeRepository;
        _propertyUnitAmenityRepository = propertyUnitAmenityRepository;
        _documentEncryptionService = documentEncryptionService;
        _configuration = configuration;
    }

    private class AmenityDto
    {
        public int AmenityId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public int PropertyId { get; set; }
        public int? PropertyUnitId { get; set; }
        public int AmenityTypeId { get; set; }
        public string AmenityName { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool RequiresBoolean { get; set; }
        public bool RequiresNumber { get; set; }
        public bool IsResidentialOnly { get; set; }
        public bool? BooleanValue { get; set; }
        public int? NumberValue { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }

        // --- 📋 ENDPOINT: GET ALL SYNDICATION PLATFORMS ---
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _externalPlatformRepository.Query().ToListAsync();
            return Ok(platforms);
        }
        [HttpGet("{propertyId}")]
        public async Task<IActionResult> GetPropertyById(int propertyId)
        {
            var role = GetCurrentCanonicalRole();
            var userTenantId = User.FindFirst("TenantId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

            var query = PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .Include(p => p.SyndicationLinks)
                    .ThenInclude(s => s.PlatformDetails)
                .Where(p => p.PropertyId == propertyId);

            query = ApplyPropertyVisibility(query);

            var property = await query.FirstOrDefaultAsync();
            if (property == null) return NotFound(new { message = "Target property record not found." });

            // A Tenant only sees their leased unit, never sibling units of the same property.
            if (role == "Tenant" && !string.IsNullOrWhiteSpace(userId))
            {
                var leasedUnitIds = await GetLeasedUnitIdsAsync(userId, userTenantId);
                property.SubUnits = property.SubUnits
                    .Where(u => leasedUnitIds.Contains(u.PropertyUnitId))
                    .ToList();
                if (!property.SubUnits.Any())
                    return NotFound(new { message = "Target property record not found." });
            }

            // 📜 Attach the currently-active sole mandate (if any) so the
            // UI can render the obligation/expiry banners without a
            // second round-trip. Null when no mandate is open.
            var activeMandate = await _soleMandateRepository.Query()
                .Where(m => m.PropertyId == propertyId && m.IsActive
                    && m.StartDate <= DateTime.UtcNow && m.EndDate >= DateTime.UtcNow)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                property,
                activeSoleMandate = activeMandate == null ? null : new
                {
                    activeMandate.SoleMandateId,
                    activeMandate.StartDate,
                    activeMandate.EndDate,
                    activeMandate.CreatedAt,
                    daysRemaining = (int)Math.Ceiling((activeMandate.EndDate - DateTime.UtcNow).TotalDays)
                }
            });
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyPayload payload)
        {
            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            Logger.LogInformation("CreateProperty: requested by {Caller}", caller);

            if (!await PermissionCheck.HasPermissionAsync("properties:create"))
            {
                Logger.LogWarning("CreateProperty: forbidden — caller {Caller} lacks properties:create permission", caller);
                return Forbid();
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
            {
                Logger.LogWarning("CreateProperty: rejected — address missing, caller={Caller}", caller);
                return BadRequest(new { message = "Property address is required." });
            }

            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId))
            {
                Logger.LogWarning("CreateProperty: forbidden — no tenant claim for caller {Caller}", caller);
                return Forbid();
            }
            if (GetCurrentCanonicalRole() == "Tenant" || GetCurrentCanonicalRole() == "Owner")
            {
                Logger.LogWarning("CreateProperty: forbidden — caller {Caller} role {Role} cannot create properties",
                    caller, GetCurrentCanonicalRole());
                return Forbid();
            }

            var isForSale = payload.IsForSale ?? string.Equals(payload.MarketClass, "Sale", StringComparison.OrdinalIgnoreCase) || string.Equals(payload.MarketClass, "Both", StringComparison.OrdinalIgnoreCase);
            var isForRent = payload.IsForRent ?? string.Equals(payload.MarketClass, "Rental", StringComparison.OrdinalIgnoreCase) || string.Equals(payload.MarketClass, "Both", StringComparison.OrdinalIgnoreCase);
            // 🧬 If the client passed explicit booleans (or the legacy string
            // maps cleanly to a single side), surface the dual-listing case in
            // the legacy MarketClass field so search and syndication see "Both".
            var marketClass = payload.MarketClass ?? "Sale";
            if (payload.IsForSale == true && payload.IsForRent == true) marketClass = "Both";
            else if (payload.IsForSale == true) marketClass = "Sale";
            else if (payload.IsForRent == true) marketClass = "Rental";

            var newProp = new PropertyAsset
            {
                TenantId = userTenantId,
                CompleteAddress = payload.CompleteAddress,
                ErfSpatialSize = payload.ErfSpatialSize ?? "200 m²",
                TitleDeedType = payload.TitleDeedType ?? "Freehold",
                PortfolioZoning = payload.PortfolioZoning ?? "Commercial Zone",
                MarketClass = marketClass,
                PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : 1,
                RoomLettingType = payload.RoomLettingType,
                ValuationPrice = payload.ValuationPrice,
                SalePrice = payload.SalePrice,
                TenantInstallationAllowance = payload.TenantInstallationAllowance,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                LinkedOwnerId = payload.LinkedOwnerId ?? "OT-0001",
                VerificationDocumentsJson = "[]",
                CreatedAt = DateTime.UtcNow,
                // 🧬 Listing flags — if the payload didn't specify them, infer
                // from the legacy MarketClass for backward compatibility.
                IsForSale = isForSale,
                IsForRent = isForRent
            };

            if (newProp.RoomLettingType != "None") newProp.PropertyTypeId = await DbContext.PropertyTypes.Where(item => item.Name == "Room share/let").Select(item => item.PropertyTypeId).SingleAsync();
            newProp.Location = await Property.Microservice.Services.Locations.PropertyLocationResolver.ResolveAsync(DbContext, payload.Location);
            newProp.GooglePlaceId = payload.GooglePlaceId;
            newProp.PostalCode = payload.PostalCode;
            if (payload.SubUnits != null && payload.SubUnits.Any())
            {
                foreach (var unitPayload in payload.SubUnits)
                {
                    newProp.SubUnits.Add(new PropertyUnit
                    {
                        UnitNumber = unitPayload.UnitNumber,
                        UnitDescription = unitPayload.UnitDescription ?? string.Empty,
                        NetRentableAreaSqM = unitPayload.NetRentableAreaSqM,
                        SpecificUnitRentalPrice = unitPayload.SpecificUnitRentalPrice,
                        SpecificUnitSalePrice = unitPayload.SpecificUnitSalePrice,
                        RentalRate = unitPayload.RentalRate,
                        AdvertisedDepositAmount = unitPayload.AdvertisedDepositAmount,
                        RentEscalationPercentage = unitPayload.RentEscalationPercentage,
                        LeaseTermMonths = unitPayload.LeaseTermMonths,
                        IsCurrentlyVacant = unitPayload.IsCurrentlyVacant,
                        UnitImagesJson = "[]",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            PropertyRepository.Add(newProp);
            await DbContext.SaveChangesAsync();

            Logger.LogInformation("Property created: PropertyId={PropertyId} Address={Address} Tenant={Tenant} By={Caller}",
                newProp.PropertyId, newProp.CompleteAddress, newProp.TenantId, caller);

            return CreatedAtAction(nameof(GetPropertyById), new { propertyId = newProp.PropertyId }, newProp);
        }
        [HttpPut("{propertyId}")]
        public async Task<IActionResult> UpdateProperty(int propertyId, [FromBody] UpdatePropertyPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                Logger.LogWarning("UpdateProperty: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
                return BadRequest(new { message = "Property address is required." });

            // 🛡️ Resolve the caller's multi-tenant scope from the JWT claims.
            // The "TenantId" claim is injected by Identity.Microservice on every issued token.
            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId))
                return Forbid();

            // Platform administrators operate across every tenant sandbox, so they
            // bypass the tenant filter. Both role aliases are honored because the
            // Identity microservice seeds the root admin as "SuperAdmin" while the
            // model/register contract advertises the registered role as "PlatformAdmin".
            var isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("SuperAdmin");

            // 🧭 Tenant scoping guard: scope the lookup to the caller's tenant so an
            // authenticated user from tenant A can never read or mutate a property
            // that belongs to tenant B. A non-owner request resolves to "not found"
            // rather than revealing cross-tenant record existence.
            IQueryable<PropertyAsset> propertyQuery = PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits);

            propertyQuery = ApplyPropertyVisibility(propertyQuery).Where(p => p.PropertyId == propertyId);

            var property = await propertyQuery.FirstOrDefaultAsync();
            if (property == null)
                return NotFound(new { message = "Target property record not found." });

            var addressChanged = property.CompleteAddress != payload.CompleteAddress || property.Latitude != payload.Latitude || property.Longitude != payload.Longitude;
            if (payload.Location is not null || addressChanged)
            {
                property.Location = await Property.Microservice.Services.Locations.PropertyLocationResolver.ResolveAsync(DbContext, payload.Location);
                property.LocationId = property.Location?.LocationId;
                property.GooglePlaceId = payload.GooglePlaceId;
                property.PostalCode = payload.PostalCode;
            }
            property.CompleteAddress = payload.CompleteAddress;
            property.ErfSpatialSize = payload.ErfSpatialSize ?? string.Empty;
            property.TitleDeedType = payload.TitleDeedType ?? "Freehold";
            property.PortfolioZoning = payload.PortfolioZoning ?? string.Empty;
            property.MarketClass = payload.MarketClass ?? "Sale";
            property.PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : property.PropertyTypeId;
            property.RoomLettingType = payload.RoomLettingType;
            if (property.RoomLettingType != "None") property.PropertyTypeId = await DbContext.PropertyTypes.Where(item => item.Name == "Room share/let").Select(item => item.PropertyTypeId).SingleAsync();
            property.ValuationPrice = payload.ValuationPrice;
            property.SalePrice = payload.SalePrice;
            property.TenantInstallationAllowance = payload.TenantInstallationAllowance;
            property.Latitude = payload.Latitude;
            property.Longitude = payload.Longitude;
            property.LinkedOwnerId = payload.LinkedOwnerId ?? string.Empty;
            // 🧬 Update listing flags — null leaves the existing value alone.
            if (payload.IsForSale.HasValue) property.IsForSale = payload.IsForSale.Value;
            if (payload.IsForRent.HasValue) property.IsForRent = payload.IsForRent.Value;
            // 🧬 Re-synthesize the legacy MarketClass string so any consumer
            // that still reads it sees a value consistent with the two flags.
            if (property.IsForSale && property.IsForRent) property.MarketClass = "Both";
            else if (property.IsForSale) property.MarketClass = "Sale";
            else if (property.IsForRent) property.MarketClass = "Rental";

            // Re-assert the tenant scope on the entity so a malicious payload cannot
            // override or clear the TenantId isolation key during a cross-tenant attempt.
            // Retain the tenant of the authorized record when an administrator edits a subagency property.

            await DbContext.SaveChangesAsync();
            return Ok(await PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .FirstAsync(p => p.PropertyId == propertyId));
        }
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations([FromQuery] string? q, CancellationToken cancellationToken)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:view")) return Forbid();
            var query = ApplyPropertyVisibility(DbContext.PropertyAssets.AsNoTracking());
            var locations = query.Where(item => item.LocationId != null).Select(item => item.Location!);
            if (!string.IsNullOrWhiteSpace(q))
                locations = locations.Where(item => item.Suburb.Contains(q) || item.City.Contains(q) || item.Region.Contains(q) || item.Country.Contains(q));
            return Ok(await locations.Distinct().OrderBy(item => item.Suburb).ThenBy(item => item.City).Take(30).ToListAsync(cancellationToken));
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchProperties([FromBody] PropertySearchCriteria criteria, [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:view"))
            {
                Logger.LogWarning("SearchProperties: forbidden — caller lacks properties:view permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            Logger.LogInformation("SearchProperties: Page={Page} PropertyType={PropertyType} ListingType={ListingType} Locations={Locations} Caller={Caller}",
                page, criteria.PropertyType, criteria.ListingType, string.Join(",", criteria.Locations ?? Enumerable.Empty<string>()), caller);

            if (page < 1) page = 1;
            // 📐 Page-size override: callers can request up to a custom cap (the
            // map + the "load all" PropertySearch view pass pageSize=10000).
            // A null/zero/negative value falls back to the default of 20.
            const int DEFAULT_PAGE_SIZE = 20;
            const int MAX_PAGE_SIZE = 10000;
            int pageSizeValue = pageSize.HasValue && pageSize.Value > 0
                ? Math.Min(pageSize.Value, MAX_PAGE_SIZE)
                : DEFAULT_PAGE_SIZE;

            // Eagerly load relational structures (Property Type lookups and sub-units)
            var query = PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .AsQueryable();

            // 🛡️ Role + tenant visibility scoping: a PlatformAdmin sees every property,
            // an Owner only their owned properties, a Tenant only active-leased properties,
            // and AgencyAdmin/Broker only their own tenant's properties.
            query = ApplyPropertyVisibility(query);

            foreach (var locationText in criteria.Locations)
            {
                var normalizedLocation = locationText.Trim().ToLowerInvariant();
                var knownLocations = await query.Where(item => item.Location != null && (item.Location.Suburb.ToLower() == normalizedLocation || item.Location.City.ToLower() == normalizedLocation))
                    .Select(item => item.LocationId).Distinct().Take(2).CountAsync();
                if (knownLocations > 1) return Conflict(new { message = "Several places have this name. Select the suggestion including its city, region and country." });
            }

            // 1. Filter by target locations array matching text boundaries
            query = Property.Microservice.Services.Locations.PropertyLocationSearchFilters.Apply(query, criteria);

            // 2. Filter by normalized Property Type lookups
            if (!string.Equals(criteria.PropertyType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.PropertyTypeDetails != null && 
                                         p.PropertyTypeDetails.Name == criteria.PropertyType);
            }

            // 📈 Listing eligibility filter (a property may be listed for SALE and/or RENT).
            if (!string.Equals(criteria.ListingType, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (criteria.ListingType.Equals("Sale", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.IsForSale);
                else if (criteria.ListingType.Equals("Rent", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.IsForRent);
            }

            if (criteria.ListingType == "Both") query = query.Where(property => property.IsForSale && property.IsForRent);
            if (criteria.ListingType == "Room") query = query.Where(property => property.IsForRent && property.RoomLettingType != "None");

            // Price range filter (uses the active listing price; sale→SalePrice, rent→unit RentalRate)
            if (criteria.MinPrice.HasValue)
                query = query.Where(p => ((criteria.ListingType == "Rent" || criteria.ListingType == "Room") ? p.SubUnits.Any(u => u.RentalRate >= criteria.MinPrice.Value) : p.SalePrice >= criteria.MinPrice.Value));
            if (criteria.MaxPrice.HasValue)
                query = query.Where(p => ((criteria.ListingType == "Rent" || criteria.ListingType == "Room") ? p.SubUnits.Any(u => u.RentalRate <= criteria.MaxPrice.Value) : p.SalePrice <= criteria.MaxPrice.Value));

            // The UI submits area bounds in square metres.  Use linked unit area
            // where available, falling back to the text field only for display.
            if (criteria.MinSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM >= criteria.MinSize.Value));
            if (criteria.MaxSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM <= criteria.MaxSize.Value));

            var total = await query.CountAsync();
            var results = await query.OrderBy(p => p.PropertyId)
                .Skip((page - 1) * pageSizeValue).Take(pageSizeValue)
                .Select(p => new
                {
                    p.PropertyId,
                    p.RoomLettingType,
                    p.IsPublicListing,
                    p.IsHotProperty,
                    p.PublicReviewStatus,
                    p.PublicSubmittedAt,
                    p.PublicPublishedAt,
                    p.PublicLettingExpiresAt,
                    p.TenantId,
                    p.CompleteAddress,
                    p.LocationId,
                    p.Location,
                    p.GooglePlaceId,
                    p.PostalCode,
                    p.ErfSpatialSize,
                    p.TitleDeedType,
                    p.PortfolioZoning,
                    p.MarketClass,
                    p.IsForSale,
                    p.IsForRent,
                    p.PropertyTypeId,
                    p.ValuationPrice,
                    p.SalePrice,
                    p.TenantInstallationAllowance,
                    p.Latitude,
                    p.Longitude,
                    p.VerificationDocumentsJson,
                    p.LinkedOwnerId,
                    p.PropertyDescription,
                    p.CreatedAt,
                    p.PropertyTypeDetails,
                    // 📜 Days remaining on the active sole mandate (if any).
                    // 0 / null = no active mandate. Surfaced on list cards so
                    // the 14-day expiration banner can fire without a
                    // second round-trip per row.
                    soleMandateDaysRemaining = p.SoleMandates
                        .Where(m => m.IsActive && m.StartDate <= DateTime.UtcNow && m.EndDate >= DateTime.UtcNow)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => (int?)Math.Ceiling((m.EndDate - DateTime.UtcNow).TotalDays))
                        .FirstOrDefault()
                })
                .ToListAsync();
                         HttpContext.Response.Headers["X-Total-Count"] = total.ToString();

            // 📊 Chart-friendly aggregates over the ENTIRE filtered universe
            // (not the 20-row page) so the listing market-share donut on the
            // dashboard reflects every property in scope.
            var propertyTypeGroups = await query
                .Where(p => p.PropertyTypeDetails != null)
                .GroupBy(p => p.PropertyTypeDetails!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();
            var listingGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ForSale"] = await query.CountAsync(p => p.IsForSale),
                ["ForRent"] = await query.CountAsync(p => p.IsForRent)
            };
            var propertyAggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["propertyType"] = propertyTypeGroups.ToDictionary(g => g.Name ?? "Unknown", g => g.Count),
                ["listingEligibility"] = listingGroups
            };
            HttpContext.Response.Headers["X-Aggregates"] = System.Text.Json.JsonSerializer.Serialize(propertyAggregates);

            Logger.LogInformation("SearchProperties: returned {Count} results (page {Page}) out of {Total} for caller {Caller}",
                results.Count, page, total, caller);
            return Ok(results);
        }
        // --- 🖼️ ENDPOINT B: IMAGE UPLOADER ---
        // Persists the uploaded picture as a data-URL entry inside the
        // property's `VerificationDocumentsJson` and emits a tiny Kafka
        // event (just the property id + filename) so downstream
        // optimisation/OCR pipelines can react. Sending the raw base64
        // through Kafka was rejected by the broker with
        // "Message size too large" — blobs belong in storage, not in event
        // streams.
        [HttpPost("{propertyId}/upload-image")]
        public async Task<IActionResult> UploadPropertyImage(int propertyId, [FromBody] ImageUploadPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                Logger.LogWarning("UploadPropertyImage: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            Logger.LogInformation("UploadPropertyImage: PropertyId={PropertyId} Caller={Caller}", propertyId, caller);
            var property = await PropertyRepository.GetByIdAsync(propertyId);
            if (property == null)
            {
                Logger.LogWarning("UploadPropertyImage: PropertyId={PropertyId} not found", propertyId);
                return NotFound(new { message = "Target property record not found." });
            }

            if (payload == null || string.IsNullOrEmpty(payload.Base64DataString))
            {
                return BadRequest(new { message = "Invalid picture data payload." });
            }

            var visibleProperty = await ApplyPropertyVisibility(PropertyRepository.Query()).AnyAsync(item => item.PropertyId == propertyId);
            if (!visibleProperty) return NotFound();
            var current = Property.Microservice.Services.Images.PropertyImageProcessor.ReadGallery(property.GalleryImageJson);
            var unitGalleries = await DbContext.PropertyUnits.Where(unit => unit.PropertyId == propertyId).Select(unit => unit.UnitImagesJson).ToListAsync();
            var unitImageCount = unitGalleries.Sum(CountUnitImages);
            if (current.Count + unitImageCount >= 10) return BadRequest(new { message = "A property can have at most 10 photographs, including unit photographs." });
            string dataUrl;
            try { dataUrl = Property.Microservice.Services.Images.PropertyImageProcessor.Process("data:image/jpeg;base64," + payload.Base64DataString); }
            catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
            current.Add(dataUrl);
            property.GalleryImageJson = JsonSerializer.Serialize(current);
            await DbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Image validated, normalised and saved to the property gallery.",
                imageCount = current.Count
            });
        }
        // --- 🔗 ENDPOINT C: PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
        private static int CountUnitImages(string gallery) => Property.Microservice.Services.Images.PropertyImageProcessor.ReadGallery(gallery).Count;


        [HttpPost("syndicate")]
        public IActionResult ToggleSyndication([FromBody] SyndicationTogglePayload payload) =>
            Conflict(new { message = "Use the Listing portals panel to configure an agency connection and request publication. Legacy simulated syndication is no longer supported." });
        [HttpPost("brochure/generate")]
        public async Task<IActionResult> GenerateBrochure([FromBody] GeneratedBrochurePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:export"))
            {
                Logger.LogWarning("GenerateBrochure: forbidden — caller lacks billing:export permission");
                return Forbid();
            }

            if (payload == null || payload.DealId <= 0 || !payload.SubUnits.Any())
            {
                Logger.LogWarning("GenerateBrochure: rejected — invalid payload (DealId={DealId} Units={Units})",
                    payload?.DealId, payload?.SubUnits?.Count ?? 0);
                return BadRequest(new { message = "A deal and at least one unit are required to generate a brochure." });
            }

            var propertyIds = payload.SubUnits.Select(u => u.PropertyId).Distinct().ToList();
            var validPropertyCount = await PropertyRepository.Query().CountAsync(p => propertyIds.Contains(p.PropertyId));
            if (validPropertyCount != propertyIds.Count)
            {
                Logger.LogWarning("GenerateBrochure: one or more properties not found — PropertyIds={PropertyIds}", propertyIds);
                return NotFound(new { message = "One or more target property records were not found." });
            }

            var canonicalRole = GetCurrentCanonicalRole();
            var isTrialClaim = User.FindFirst("IsTrial")?.Value;
            var trialEndDateClaim = User.FindFirst("TrialEndDate")?.Value;

            if (isTrialClaim == "true" && (canonicalRole == "AgencyAdmin" || canonicalRole == "Broker"))
            {
                Logger.LogWarning("GenerateBrochure: blocked — trial restriction for Role={Role} DealId={DealId}",
                    canonicalRole, payload.DealId);
                return Forbid(new
                {
                    message = "Brochure compilation is restricted during the trial period.",
                    trialEndDate = trialEndDateClaim
                }.ToString());
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var tenantId = User.FindFirst("TenantId")?.Value ?? string.Empty;

             var crmPayload = new
             {
                 dealId = payload.DealId,
                 brochureCardId = payload.BrochureCardId,
                 propertyId = payload.SubUnits.FirstOrDefault()?.PropertyId ?? 0,
                 tenantId = tenantId,
                 requestedBy = caller,
                 brokerName = payload.BrokerName,
                 brokerEmail = payload.BrokerEmail,
                 brokerPhone = payload.BrokerPhone,
                 companyName = payload.CompanyName,
                 companyLogoUrl = payload.CompanyLogoUrl,
                 clientName = payload.ClientName,
                 clientEmail = payload.ClientEmail,
                 clientPhone = payload.ClientPhone,
                 marketClass = payload.MarketClass,
                 tenantInstallationAllowance = payload.TenantInstallationAllowance,
                 subUnits = payload.SubUnits.Select(u => new
                 {
                     propertyId = u.PropertyId,
                     propertyDescription = u.PropertyDescription,
                     latitude = u.Latitude,
                     longitude = u.Longitude,
                     unitNumber = u.UnitNumber,
                     unitDescription = u.UnitDescription,
                     netRentableAreaSqM = u.NetRentableAreaSqM,
                     rentalRate = u.RentalRate,
                     rentEscalationPercentage = u.RentEscalationPercentage,
                     leaseTermMonths = u.LeaseTermMonths,
                     salePrice = u.SalePrice,
                     selectedImageRefs = u.SelectedImageRefs,
                     propertyType = u.PropertyType,
                     valuationPrice = u.ValuationPrice,
                     erfSpatialSize = u.ErfSpatialSize,
                     titleDeedType = u.TitleDeedType,
                     portfolioZoning = u.PortfolioZoning,
                     isForSale = u.IsForSale,
                     isForRent = u.IsForRent,
                     isCurrentlyVacant = u.IsCurrentlyVacant,
                     amenities = u.Amenities
                 }).ToList()
             };

            // Call CRM microservice via gateway to queue brochure generation
            var gatewayUrl = _configuration["Gateway:BaseUrl"]
                ?? Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
                ?? "http://gateway:5000/gateway";
            using var httpClient = new HttpClient { BaseAddress = new Uri(gatewayUrl) };
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));

            var response = await httpClient.PostAsJsonAsync("/api/crm/brochure/generate", crmPayload);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("GenerateBrochure: CRM call failed — {StatusCode} {Error}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, new { message = "Failed to queue brochure generation." });
            }

            Logger.LogInformation("GenerateBrochure: queued via CRM — DealId={DealId} Units={Units}", payload.DealId, payload.SubUnits.Count);
            return Accepted(new { message = "Brochure generation request queued." });
        }
    // 📊 Active-listing count + average DOM for the broker dashboard.
    // `tenantId` may be a comma-separated list of tenantIds so the caller
    // can pass an agency subtree in one go.
    [HttpGet("active-listings-count")]
    public async Task<IActionResult> GetActiveListingsCount(
        [FromQuery] string? tenantId = null,
        [FromQuery] string? linkedBrokerId = null)
    {
        var tenantIds = ParseTenantList(tenantId);
        var q = ApplyPropertyVisibility(PropertyRepository.Query())
            .Where(property => property.IsForSale || property.IsForRent);
        if (tenantIds.Count > 0)
            q = q.Where(property => tenantIds.Contains(property.TenantId));
        var count = await q.CountAsync();
        if (count == 0) return Ok(new { activeListings = 0, averageDom = 0 });
        var avg = await q.Select(p => (double)EF.Functions.DateDiffDay(p.CreatedAt, DateTime.UtcNow)).AverageAsync();
        return Ok(new { activeListings = count, averageDom = (int)Math.Round(avg) });
    }

    // 📊 Sole mandate count for the AgencyGlance KPIs.
    // Accepts either comma-separated `tenantId=TEN-1,TEN-2` or repeated
    // `tenantId=TEN-1&tenantId=TEN-2` query parameters.
    [HttpGet("stats/sole-mandates")]
    public async Task<IActionResult> GetSoleMandatesCount([FromQuery] string? tenantId = null)
    {
        var tenantIds = ParseTenantList(tenantId);
        IQueryable<PropertyAsset> query = ApplyPropertyVisibility(PropertyRepository.Query())
            .Where(p => p.SoleMandates.Any(sm => sm.IsActive));

        if (tenantIds.Count > 0)
        {
            query = query.Where(p => tenantIds.Contains(p.TenantId));
        }

        var count = await query.CountAsync();
        return Ok(new { totalSoleMandates = count });
    }

    // Cumulative property count at each month end; current month is counted to date.
    // Accepts the same tenantId formats as sole-mandates.
    [HttpGet("stats/growth")]
    public async Task<IActionResult> GetPropertiesGrowth(
        [FromQuery] string? tenantId = null,
        [FromQuery] int months = 6)
    {
        var tenantIds = ParseTenantList(tenantId);
        months = Math.Clamp(months, 1, 24);
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1 - months);
        var query = ApplyPropertyVisibility(PropertyRepository.Query());
        if (tenantIds.Count > 0)
            query = query.Where(property => tenantIds.Contains(property.TenantId));

        // Include records created before the visible window. Historical totals
        // describe surviving records, since deleted-property history is not stored.
        var monthly = new List<PropertyCountAtMonth>();
        for (var index = 0; index < months; index++)
        {
            var month = firstMonth.AddMonths(index);
            var endExclusive = month.AddMonths(1);
            var count = await query.CountAsync(property => property.CreatedAt < endExclusive && property.CreatedAt <= now);
            monthly.Add(new PropertyCountAtMonth(month.Year, month.Month, count));
        }

        return Ok(monthly);
    }

    private static List<string> ParseTenantList(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return new List<string>();

        return tenantId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();
    }
    // 📦 List every amenity (both scopes) for a property, decorated with the
    // type metadata the frontend uses to render toggles/numbers.
    [HttpGet("{propertyId:int}/amenities")]
    public async Task<IActionResult> GetPropertyAmenities(int propertyId)
    {
        var property = await PropertyRepository.Query()
            .Include(p => p.PropertyTypeDetails)
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var isResidential = property.PropertyTypeDetails?.Name is "Residential" or "Room share/let";

        var rows = await _propertyUnitAmenityRepository.Query()
            .Where(a => a.PropertyId == propertyId && a.IsActive)
            .Include(a => a.AmenityType)
            .Select(a => new AmenityDto
            {
                AmenityId = a.AmenityId,
                TenantId = a.TenantId,
                PropertyId = a.PropertyId,
                PropertyUnitId = a.PropertyUnitId,
                AmenityTypeId = a.AmenityTypeId,
                AmenityName = a.AmenityType!.Name,
                Label = a.AmenityType.Label,
                RequiresBoolean = a.AmenityType.RequiresBoolean,
                RequiresNumber = a.AmenityType.RequiresNumber,
                IsResidentialOnly = a.AmenityType.IsResidentialOnly,
                BooleanValue = a.BooleanValue,
                NumberValue = a.NumberValue,
                Notes = a.Notes,
                IsActive = a.IsActive
            })
            .ToListAsync();

        // Tell the UI whether unit-level add is allowed for this property.
        return Ok(new
        {
            isResidential,
            canAddUnitAmenities = !isResidential,
            items = rows
        });
    }
    // 🅰️ Upsert a single amenity. `unitId` is optional; when omitted the
    // amenity is property-level. Residential assets always get property-level
    // amenities only — passing a unitId on a residential property is rejected.
    [HttpPost("{propertyId:int}/amenities")]
    public async Task<IActionResult> UpsertPropertyAmenity(int propertyId, [FromQuery] int? unitId, [FromBody] AmenityUpsertDto dto)
    {
        var property = await PropertyRepository.Query()
            .Include(p => p.PropertyTypeDetails)
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var isResidential = property.PropertyTypeDetails?.Name is "Residential" or "Room share/let";
        if (isResidential && unitId.HasValue)
        {
            return Conflict(new { message = "Residential properties cannot have unit-level amenities." });
        }

        var amenityType = await _amenityTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.AmenityTypeId == dto.AmenityTypeId && t.IsActive);
        if (amenityType == null) return BadRequest(new { message = "Invalid or inactive amenity type." });

        // Residential-only amenity types may only attach to residential assets.
        if (amenityType.IsResidentialOnly && !isResidential)
        {
            return Conflict(new { message = amenityType.Name + " is only valid for residential properties." });
        }

        if (unitId.HasValue)
        {
            var unit = await PropertyUnitRepository.Query().FirstOrDefaultAsync(u => u.PropertyUnitId == unitId.Value && u.PropertyId == propertyId);
            if (unit == null) return BadRequest(new { message = "Unit not found on this property." });
        }

        var existing = await _propertyUnitAmenityRepository.Query()
            .FirstOrDefaultAsync(a => a.PropertyId == propertyId
                && a.AmenityTypeId == dto.AmenityTypeId
                && a.PropertyUnitId == unitId);

        if (existing == null)
        {
            existing = new PropertyUnitAmenity
            {
                TenantId = property.TenantId,
                PropertyId = propertyId,
                PropertyUnitId = unitId,
                AmenityTypeId = dto.AmenityTypeId,
                IsActive = true
            };
            _propertyUnitAmenityRepository.Add(existing);
        }

        if (amenityType.RequiresBoolean) existing.BooleanValue = dto.BooleanValue;
        if (amenityType.RequiresNumber)  existing.NumberValue = dto.NumberValue;
        existing.Notes = dto.Notes;

            await DbContext.SaveChangesAsync();
            await DbContext.Entry(existing).Reference(a => a.AmenityType).LoadAsync();
            return Ok(new
            {
                existing.AmenityId,
                existing.PropertyId,
                existing.PropertyUnitId,
                amenityName = amenityType.Name,
                amenityType.Label,
                amenityType.RequiresBoolean,
                amenityType.RequiresNumber,
                amenityType.IsResidentialOnly,
                existing.BooleanValue,
                existing.NumberValue,
                existing.Notes,
                existing.IsActive
            });
    }
    // 🗑️ Soft-delete (deactivate) an amenity. Hard deletes would orphan audit; we keep the row.
    [HttpDelete("{propertyId:int}/amenities/{amenityId:int}")]
    public async Task<IActionResult> DeletePropertyAmenity(int propertyId, int amenityId)
    {
        var amenity = await _propertyUnitAmenityRepository.Query()
            .FirstOrDefaultAsync(a => a.AmenityId == amenityId && a.PropertyId == propertyId && a.IsActive);
        if (amenity == null) return NotFound(new { message = "Amenity not found." });
        amenity.IsActive = false;
        await DbContext.SaveChangesAsync();
        return Ok(new { message = "Amenity removed." });
    }
    // 📄 Serve a single verification document by index. Supports both
    // inline data URLs and on-disk file paths produced by the image worker.
    [HttpGet("{propertyId:int}/documents/{index:int}")]
    public async Task<IActionResult> GetPropertyDocument(int propertyId, int index)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:view")) return Forbid();
        var property = await ApplyPropertyVisibility(PropertyRepository.Query())
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var documents = DeserializeVerificationDocuments(property.VerificationDocumentsJson);

        if (index < 0 || index >= documents.Count) return NotFound(new { message = "Document not found." });

        var documentReference = documents[index];
        var metadata = ParseVerificationDocumentMetadata(property.VerificationDocumentMetadataJson);
        var documentMetadata = index < metadata.Count ? metadata[index] : null;

        if (documentReference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = documentReference.Split(',', 2);
            if (parts.Length != 2) return BadRequest(new { message = "Invalid data URL." });

            var header = parts[0].Split(';');
            var mime = header[0].Replace("data:", "", StringComparison.OrdinalIgnoreCase);
            var dataBytes = Convert.FromBase64String(parts[1]);
            var contentType = documentMetadata?.ContentType ?? mime;
            var fileName = string.IsNullOrWhiteSpace(documentMetadata?.FileName) ? null : documentMetadata.FileName;
            return File(dataBytes, contentType, fileName);
        }

        var filePath = ResolveStoredDocumentPath(documentMetadata?.StoredName ?? documentReference);
        if (filePath == null) return BadRequest(new { message = "Invalid document path." });
        if (!System.IO.File.Exists(filePath)) return NotFound(new { message = "Document file not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var storedFileName = Path.GetFileName(filePath);
        var storedContentType = documentMetadata?.ContentType ?? GetStoredDocumentContentType(storedFileName);
        var downloadFileName = string.IsNullOrWhiteSpace(documentMetadata?.FileName) ? storedFileName : documentMetadata.FileName;
        return File(bytes, storedContentType, downloadFileName);
    }

    private string? ResolveStoredDocumentPath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var normalizedPath = storedPath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var storageRoot = Path.GetFullPath(StorageRootPath);
        var candidatePaths = Path.IsPathRooted(normalizedPath)
            ? new[] { Path.GetFullPath(normalizedPath) }
            : new[]
            {
                Path.GetFullPath(Path.Combine(storageRoot, normalizedPath)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PropertyMediaVault", normalizedPath))
            };

        foreach (var candidatePath in candidatePaths)
        {
            if (!IsPathWithinStorageRoot(candidatePath, storageRoot)) continue;
            if (System.IO.File.Exists(candidatePath)) return candidatePath;
        }

        return candidatePaths.FirstOrDefault(candidatePath => IsPathWithinStorageRoot(candidatePath, storageRoot));
    }

    private static bool IsPathWithinStorageRoot(string candidatePath, string storageRoot)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(storageRoot);
        return candidatePath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStoredDocumentContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }


    // ➕ Upload a new verification document into the property's deeds vault.
    // The document is stored as a base64 data URL inside VerificationDocumentsJson
    // so it survives in a single row without needing a separate file table.
    [HttpPost("{propertyId:int}/documents")]
    public async Task<IActionResult> UploadPropertyDocument(int propertyId, [FromForm] UploadPropertyDocumentDto dto)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(PropertyRepository.Query())
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        if (dto.File == null || dto.File.Length == 0)
            return BadRequest(new { message = "A file is required." });

        if (dto.IsPasswordProtected && string.IsNullOrWhiteSpace(dto.DocumentPassword))
            return BadRequest(new { message = "A document password is required when password protection is enabled." });

        const long maxBytes = 10 * 1024 * 1024;
        if (dto.File.Length > maxBytes)
            return BadRequest(new { message = $"File exceeds the 10 MB size limit. Selected file is {dto.File.Length / (1024.0 * 1024.0):0.0} MB." });

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".gif", ".doc", ".docx", ".txt"
        };
        var extension = Path.GetExtension(dto.File.FileName);
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            return BadRequest(new { message = $"File type '{extension}' is not supported. Allowed: {string.Join(", ", allowedExtensions)}." });

        EncryptedDocumentResult documentResult;
        try
        {
            await using var sourceStream = dto.File.OpenReadStream();
            documentResult = await _documentEncryptionService.EncryptAsync(
                sourceStream,
                dto.File.FileName,
                dto.DocumentPassword,
                dto.IsPasswordProtected);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Verification document encryption failed for PropertyId={PropertyId}", propertyId);
            return BadRequest(new { message = "The document could not be prepared for upload. Please check the file and password, then try again." });
        }

        var documentPath = string.Empty;
        if (documentResult.IsPasswordProtected)
        {
            try
            {
                documentPath = await SaveEncryptedDocumentAsync(propertyId, documentResult);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Password-protected verification document storage failed for PropertyId={PropertyId}", propertyId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The document was encrypted but could not be saved. Please try again."
                });
            }
        }

        var documents = DeserializeVerificationDocuments(property.VerificationDocumentsJson);
        var metadata = ParseVerificationDocumentMetadata(property.VerificationDocumentMetadataJson);
        var documentName = string.IsNullOrWhiteSpace(dto.Name) ? Path.GetFileNameWithoutExtension(dto.File.FileName) : dto.Name.Trim();
        var documentMetadata = new DocumentMeta
        {
            Name = documentName,
            Description = dto.Description?.Trim() ?? string.Empty,
            FileName = documentResult.FileName,
            StoredName = documentPath,
            ContentType = documentResult.ContentType,
            SizeBytes = documentResult.Content.Length,
            UploadedAt = DateTime.UtcNow,
            IsPasswordProtected = documentResult.IsPasswordProtected
        };

        if (documentResult.IsPasswordProtected)
        {
            documents.Add(documentPath);
        }
        else
        {
            var base64 = Convert.ToBase64String(documentResult.Content);
            documents.Add($"data:{documentResult.ContentType};base64,{base64}");
        }

        metadata.Add(documentMetadata);
        property.VerificationDocumentsJson = JsonSerializer.Serialize(documents);
        property.VerificationDocumentMetadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        await DbContext.SaveChangesAsync();

        return Ok(new
        {
            message = documentResult.IsPasswordProtected
                ? "Password-protected verification document uploaded."
                : "Verification document uploaded.",
            index = documents.Count - 1,
            isPasswordProtected = documentResult.IsPasswordProtected
        });
    }

    private async Task<string> SaveEncryptedDocumentAsync(int propertyId, EncryptedDocumentResult documentResult)
    {
        var documentFolder = Path.Combine(StorageRootPath, $"Property_{propertyId}", "Documents");
        Directory.CreateDirectory(documentFolder);

        var storedFileName = $"{Guid.NewGuid():N}{documentResult.FileExtension}";
        var storedFilePath = Path.Combine(documentFolder, storedFileName);
        await System.IO.File.WriteAllBytesAsync(storedFilePath, documentResult.Content);

        return Path.Combine($"Property_{propertyId}", "Documents", storedFileName)
            .Replace('\\', '/');
    }

    private static List<string> DeserializeVerificationDocuments(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }


    // ❌ Delete a verification document from the property's deeds vault.
    [HttpDelete("{propertyId:int}/documents/{index:int}")]
    public async Task<IActionResult> DeletePropertyDocument(int propertyId, int index)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(PropertyRepository.Query())
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var documents = DeserializeVerificationDocuments(property.VerificationDocumentsJson);

        if (index < 0 || index >= documents.Count) return NotFound(new { message = "Document not found." });

        var metadata = ParseVerificationDocumentMetadata(property.VerificationDocumentMetadataJson);
        var documentMetadata = index < metadata.Count ? metadata[index] : null;
        var documentReference = documents[index];
        var storedFilePath = documentMetadata == null || !documentMetadata.IsPasswordProtected
            ? null
            : ResolveStoredDocumentPath(documentMetadata.StoredName ?? documentReference);

        if (storedFilePath != null && System.IO.File.Exists(storedFilePath))
        {
            System.IO.File.Delete(storedFilePath);
        }

        documents.RemoveAt(index);
        if (index < metadata.Count) metadata.RemoveAt(index);

        property.VerificationDocumentsJson = JsonSerializer.Serialize(documents);
        property.VerificationDocumentMetadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        await DbContext.SaveChangesAsync();

        return Ok(new { message = "Verification document removed." });
    }
}
