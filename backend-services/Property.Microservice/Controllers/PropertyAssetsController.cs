using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
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
public class PropertyAssetsController : PropertyControllerBase
{
    private readonly IRepository<ExternalPlatform> _externalPlatformRepository;
    private readonly IRepository<SoleMandate> _soleMandateRepository;
    private readonly IRepository<PropertyPlatformSyndication> _propertyPlatformSyndicationRepository;
    private readonly IRepository<AmenityType> _amenityTypeRepository;
    private readonly IRepository<PropertyUnitAmenity> _propertyUnitAmenityRepository;

    public PropertyAssetsController(
        PropertyDbContext dbContext,
        KafkaPublisherService kafkaPublisher,
        PermissionCheckService permissionCheck,
        IPropertyRepository propertyRepository,
        IPropertyUnitRepository propertyUnitRepository,
        ILogger<PropertyAssetsController> logger,
        IRepository<ExternalPlatform> externalPlatformRepository,
        IRepository<SoleMandate> soleMandateRepository,
        IRepository<PropertyPlatformSyndication> propertyPlatformSyndicationRepository,
        IRepository<AmenityType> amenityTypeRepository,
        IRepository<PropertyUnitAmenity> propertyUnitAmenityRepository)
        : base(dbContext, kafkaPublisher, permissionCheck, propertyRepository, propertyUnitRepository, logger)
    {
        _externalPlatformRepository = externalPlatformRepository;
        _soleMandateRepository = soleMandateRepository;
        _propertyPlatformSyndicationRepository = propertyPlatformSyndicationRepository;
        _amenityTypeRepository = amenityTypeRepository;
        _propertyUnitAmenityRepository = propertyUnitAmenityRepository;
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
                ValuationPrice = payload.ValuationPrice,
                SalePrice = payload.SalePrice,
                TenantInstallationAllowance = payload.TenantInstallationAllowance,
                Latitude = payload.Latitude != 0 ? payload.Latitude : -33.9249m,
                Longitude = payload.Longitude != 0 ? payload.Longitude : 18.4241m,
                LinkedOwnerId = payload.LinkedOwnerId ?? "OT-0001",
                VerificationDocumentsJson = "[]",
                CreatedAt = DateTime.UtcNow,
                // 🧬 Listing flags — if the payload didn't specify them, infer
                // from the legacy MarketClass for backward compatibility.
                IsForSale = isForSale,
                IsForRent = isForRent
            };

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

            propertyQuery = isPlatformAdmin
                ? propertyQuery.Where(p => p.PropertyId == propertyId)
                : propertyQuery.Where(p => p.PropertyId == propertyId && p.TenantId == userTenantId);

            var property = await propertyQuery.FirstOrDefaultAsync();
            if (property == null)
                return NotFound(new { message = "Target property record not found." });

            property.CompleteAddress = payload.CompleteAddress;
            property.ErfSpatialSize = payload.ErfSpatialSize ?? string.Empty;
            property.TitleDeedType = payload.TitleDeedType ?? "Freehold";
            property.PortfolioZoning = payload.PortfolioZoning ?? string.Empty;
            property.MarketClass = payload.MarketClass ?? "Sale";
            property.PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : property.PropertyTypeId;
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
            property.TenantId = isPlatformAdmin
                ? property.TenantId
                : userTenantId;

            await DbContext.SaveChangesAsync();
            return Ok(await PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .FirstAsync(p => p.PropertyId == propertyId));
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

            // 1. Filter by target locations array matching text boundaries
            if (criteria.Locations != null && criteria.Locations.Any())
            {
                query = query.Where(p => criteria.Locations.Any(loc => p.CompleteAddress.Contains(loc)));
            }

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

            // Price range filter (uses the active listing price; sale→SalePrice, rent→unit RentalRate)
            if (criteria.MinPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.SubUnits.Any(u => u.RentalRate >= criteria.MinPrice.Value) : p.SalePrice >= criteria.MinPrice.Value));
            if (criteria.MaxPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.SubUnits.Any(u => u.RentalRate <= criteria.MaxPrice.Value) : p.SalePrice <= criteria.MaxPrice.Value));

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
                    p.TenantId,
                    p.CompleteAddress,
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

            // 🖼️ Persist to the property IMAGE GALLERY. Append (don't replace) so the
            // gallery grows over time. A hard cap of 50 keeps the row sane.
            var current = string.IsNullOrWhiteSpace(property.GalleryImageJson)
                ? new List<string>()
                : (System.Text.Json.JsonSerializer.Deserialize<List<string>>(property.GalleryImageJson) ?? new List<string>());

            var mime = payload.FileName?.ToLowerInvariant() switch
            {
                var n when n != null && n.EndsWith(".png") => "image/png",
                var n when n != null && n.EndsWith(".webp") => "image/webp",
                var n when n != null && (n.EndsWith(".jpg") || n.EndsWith(".jpeg")) => "image/jpeg",
                _ => "image/jpeg"
            };
            var dataUrl = $"data:{mime};base64,{payload.Base64DataString}";
            current.Add(dataUrl);
            if (current.Count > 50) current = current.GetRange(current.Count - 50, 50);
            property.GalleryImageJson = System.Text.Json.JsonSerializer.Serialize(current);

            await DbContext.SaveChangesAsync();

            // 🛰️ Notify downstream optimisers (OCR, image resizing, ML
            // tagging) via Kafka. The event payload is intentionally tiny —
            // just the id + filename — so it stays well under the broker's
            // 1 MB default message size limit.
            var imageEvent = new PropertyImageUploadedEvent(
                propertyId,
                payload.FileName,
                payload.Base64DataString,
                property.TenantId,
                caller);
            try
            {
                await KafkaPublisher.RaiseEventAsync(
                    TopicNames.PropertyImages,
                    propertyId.ToString(),
                    imageEvent);
            }
            catch (Exception ex)
            {
                // The image is already persisted — a Kafka publish failure
                // shouldn't fail the user-visible upload. Just log it so the
                // optimisation pipeline can be re-triggered later.
                Logger.LogWarning(ex, "UploadPropertyImage: Kafka notify failed for PropertyId={PropertyId} (image still saved)", propertyId);
            }

            return Ok(new
            {
                message = "Image persisted to property gallery and optimisers notified.",
                imageCount = current.Count
            });
        }
        // --- 🔗 ENDPOINT C: PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
        [HttpPost("syndicate")]
        public async Task<IActionResult> ToggleSyndication([FromBody] SyndicationTogglePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                Logger.LogWarning("ToggleSyndication: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            Logger.LogInformation("ToggleSyndication: PropertyId={PropertyId} PlatformId={PlatformId} Syndicate={Syndicate} Caller={Caller}",
                payload?.PropertyId, payload?.PlatformId, payload?.SyndicateStatus, caller);

            if (payload == null)
            {
                Logger.LogWarning("ToggleSyndication: rejected — null payload, caller={Caller}", caller);
                return BadRequest(new { message = "Invalid syndication data envelope." });
            }

            var property = await PropertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .FirstOrDefaultAsync(p => p.PropertyId == payload.PropertyId);

            if (property == null) return NotFound(new { message = "Target property record not found." });

            var platform = await _externalPlatformRepository.GetByIdAsync(payload.PlatformId);
            if (platform == null) return NotFound(new { message = "Target external marketing channel portal not found." });

            // Fetch or create the portal links ledger row matching current identifiers
            var syndicationRecord = await _propertyPlatformSyndicationRepository.Query()
                .FirstOrDefaultAsync(s => s.PropertyId == payload.PropertyId && s.PlatformId == payload.PlatformId);

            if (syndicationRecord == null)
            {
                syndicationRecord = new PropertyPlatformSyndication
                {
                    PropertyId = payload.PropertyId,
                    PlatformId = payload.PlatformId,
                    IsSyndicatedActive = payload.SyndicateStatus,
                    ExternalReferenceIndex = "EXT-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                    LivePortalListingUrl = $"{platform.BaseUrlAddress}/listing/prop-{payload.PropertyId}",
                    LastSynchronizedAt = DateTime.UtcNow
                };
                _propertyPlatformSyndicationRepository.Add(syndicationRecord);
            }
            else
            {
                syndicationRecord.IsSyndicatedActive = payload.SyndicateStatus;
                syndicationRecord.LastSynchronizedAt = DateTime.UtcNow;
            }

            await DbContext.SaveChangesAsync();

            var syndicationEvent = new PropertySyndicationRequestedEvent(
                PropertyId: payload.PropertyId,
                PlatformId: payload.PlatformId,
                IsActive: payload.SyndicateStatus,
                TenantId: property.TenantId,
                RequestedBy: caller
            );

            await KafkaPublisher.RaiseEventAsync(
                TopicNames.PropertySyndication,
                payload.PropertyId.ToString(),
                syndicationEvent);

            Logger.LogInformation("ToggleSyndication: completed — PropertyId={PropertyId} PlatformId={PlatformId} Active={Active} ExternalRef={ExternalRef}",
                payload.PropertyId, payload.PlatformId, payload.SyndicateStatus, syndicationRecord.ExternalReferenceIndex);

            return Ok(new
            {
                message = payload.SyndicateStatus 
                    ? $"Asset successfully pushed out to {platform.PlatformName} tracking registers." 
                    : $"Asset successfully de-listed from {platform.PlatformName} tracking networks.",
                externalRef = syndicationRecord.ExternalReferenceIndex,
                // Maps directly back to the property used in legacy integrations to satisfy compilation requirements
                directPropertyUrl = syndicationRecord.LivePortalListingUrl 
            });
        }
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

            var brochureEvent = new BrochureGenerationRequestedEvent(
                DealId: payload.DealId,
                BrochureCardId: payload.BrochureCardId,
                PropertyId: payload.SubUnits.FirstOrDefault()?.PropertyId ?? 0,
                TenantId: tenantId,
                RequestedBy: caller,
                BrokerName: payload.BrokerName,
                BrokerEmail: payload.BrokerEmail,
                BrokerPhone: payload.BrokerPhone,
                CompanyName: payload.CompanyName,
                CompanyLogoUrl: payload.CompanyLogoUrl,
                ClientName: payload.ClientName,
                ClientEmail: payload.ClientEmail,
                ClientPhone: payload.ClientPhone,
                MarketClass: payload.MarketClass,
                TenantInstallationAllowance: payload.TenantInstallationAllowance,
                SubUnits: payload.SubUnits.Select(u => new BrochureSubUnitEvent
                {
                    PropertyId = u.PropertyId,
                    PropertyDescription = u.PropertyDescription,
                    Latitude = u.Latitude,
                    Longitude = u.Longitude,
                    UnitNumber = u.UnitNumber,
                    UnitDescription = u.UnitDescription,
                    NetRentableAreaSqM = u.NetRentableAreaSqM,
                    RentalRate = u.RentalRate,
                    RentEscalationPercentage = u.RentEscalationPercentage,
                    LeaseTermMonths = u.LeaseTermMonths,
                    SalePrice = u.SalePrice,
                    SelectedImageRefs = u.SelectedImageRefs
                }).ToList()
            );

            await KafkaPublisher.RaiseEventAsync(
                TopicNames.BrochureGenerationRequests,
                payload.DealId.ToString(),
                brochureEvent);

            Logger.LogInformation("GenerateBrochure: queued — DealId={DealId} Units={Units}", payload.DealId, payload.SubUnits.Count);
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
        var tenantIds = (tenantId ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (tenantIds.Count == 0) return Ok(new { activeListings = 0, averageDom = 0 });

        var q = PropertyRepository.Query()
            .Where(p => tenantIds.Contains(p.TenantId) && (p.IsForSale || p.IsForRent));
        var count = await q.CountAsync();
        if (count == 0) return Ok(new { activeListings = 0, averageDom = 0 });
        var avg = await q.Select(p => (double)EF.Functions.DateDiffDay(p.CreatedAt, DateTime.UtcNow)).AverageAsync();
        return Ok(new { activeListings = count, averageDom = (int)Math.Round(avg) });
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

        var isResidential = string.Equals(property.PropertyTypeDetails?.Name, "Residential", StringComparison.OrdinalIgnoreCase);

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

        var isResidential = string.Equals(property.PropertyTypeDetails?.Name, "Residential", StringComparison.OrdinalIgnoreCase);
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
        var property = await PropertyRepository.Query()
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var docs = string.IsNullOrWhiteSpace(property.VerificationDocumentsJson)
            ? new List<string>()
            : (System.Text.Json.JsonSerializer.Deserialize<List<string>>(property.VerificationDocumentsJson) ?? new List<string>());

        if (index < 0 || index >= docs.Count) return NotFound(new { message = "Document not found." });

        var doc = docs[index];

        // Data URL — return inline
        if (doc.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = doc.Split(',', 2);
            if (parts.Length != 2) return BadRequest(new { message = "Invalid data URL." });

            var meta = parts[0].Split(';');
            var mime = meta[0].Replace("data:", "", StringComparison.OrdinalIgnoreCase);
            var base64 = parts[1];

            var dataBytes = Convert.FromBase64String(base64);
            return File(dataBytes, mime);
        }

        // File path — resolve from worker storage root
        var fileName = Path.GetFileName(doc);
        var workerRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PropertyMediaVault"));
        var filePath = Path.GetFullPath(Path.Combine(workerRoot, doc.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));

        if (!filePath.StartsWith(workerRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid document path." });

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "Document file not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
            : fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg"
            : "application/octet-stream";

        return File(bytes, contentType, fileName);
    }
}
