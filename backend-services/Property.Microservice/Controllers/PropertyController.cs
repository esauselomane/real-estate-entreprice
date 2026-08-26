using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Property.Microservice.Controllers
{
    [ApiController]
    [Route("api/properties")]
    public class PropertyController : ControllerBase
    {
        private readonly PropertyDbContext _context;
        private readonly KafkaPublisherService _kafkaPublisher;

        public PropertyController(PropertyDbContext context, KafkaPublisherService kafkaPublisher)
        {
            _context = context;
            _kafkaPublisher = kafkaPublisher;
        }

        // --- 📥 DATA TRANSFER INTERFACE MODEL BINDINGS ---
        public class PropertySearchCriteria
        {
            public List<string> Locations { get; set; } = new List<string>();
            public string PropertyType { get; set; } = "All"; // All, Residential, Commercial, Industrial, Retail
            public int? MinSize { get; set; }
            public int? MaxSize { get; set; }
        }

        public class ImageUploadPayload
        {
            public string FileName { get; set; } = string.Empty;
            public string Base64DataString { get; set; } = string.Empty;
        }

        public class SyndicationTogglePayload
        {
            public int PropertyId { get; set; }
            public int PlatformId { get; set; }
            public bool SyndicateStatus { get; set; }
        }

        public class CreatePropertyUnitPayload
        {
            public string UnitNumber { get; set; } = string.Empty;
            public string UnitDescription { get; set; } = string.Empty;
            public decimal NetRentableAreaSqM { get; set; }
            public decimal? SpecificUnitRentalPrice { get; set; }
            public decimal? SpecificUnitSalePrice { get; set; }
            public bool IsCurrentlyVacant { get; set; } = true;
        }

        public class CreatePropertyPayload
        {
            public string CompleteAddress { get; set; } = string.Empty;
            public string ErfSpatialSize { get; set; } = string.Empty;
            public string TitleDeedType { get; set; } = "Freehold";
            public string PortfolioZoning { get; set; } = "Commercial Zone";
            public string MarketClass { get; set; } = "Sale";
            public int PropertyTypeId { get; set; } = 1;
            public decimal ValuationPrice { get; set; }
            public decimal? SalePrice { get; set; }
            public decimal? BaseRentalRate { get; set; }
            public decimal? RentEscalationPercentage { get; set; }
            public int? LeaseTermMonths { get; set; }
            public decimal TenantInstallationAllowance { get; set; } = 0;
            public decimal Latitude { get; set; } = -33.9249m;
            public decimal Longitude { get; set; } = 18.4241m;
            public string LinkedOwnerId { get; set; } = "OT-0001";
            public List<CreatePropertyUnitPayload> SubUnits { get; set; } = new List<CreatePropertyUnitPayload>();
        }

        // --- 📋 ENDPOINT: GET ALL SYNDICATION PLATFORMS ---
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _context.ExternalPlatforms.ToListAsync();
            return Ok(platforms);
        }

        // --- 🔍 ENDPOINT: GET SINGLE PROPERTY WITH LINKED SUB-UNITS ---
        [HttpGet("{propertyId}")]
        public async Task<IActionResult> GetPropertyById(int propertyId)
        {
            var property = await _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .Include(p => p.SyndicationLinks)
                    .ThenInclude(s => s.PlatformDetails)
                .FirstOrDefaultAsync(p => p.PropertyId == propertyId);

            if (property == null) return NotFound(new { message = "Target property record not found." });
            return Ok(property);
        }

        // --- 🏢 ENDPOINT: GET SUB-UNITS FOR A PROPERTY ---
        [HttpGet("{propertyId}/units")]
        public async Task<IActionResult> GetPropertyUnits(int propertyId)
        {
            var propertyExists = await _context.PropertyAssets.AnyAsync(p => p.PropertyId == propertyId);
            if (!propertyExists) return NotFound(new { message = "Target property record not found." });

            var units = await _context.PropertyUnits
                .Where(u => u.PropertyId == propertyId)
                .OrderBy(u => u.UnitNumber)
                .ToListAsync();

            return Ok(units);
        }

        // --- ➕ ENDPOINT: ADD SUB-UNIT TO PROPERTY ---
        [HttpPost("{propertyId}/units")]
        public async Task<IActionResult> AddPropertyUnit(int propertyId, [FromBody] CreatePropertyUnitPayload payload)
        {
            var property = await _context.PropertyAssets.FindAsync(propertyId);
            if (property == null) return NotFound(new { message = "Target property record not found." });

            if (payload == null || string.IsNullOrWhiteSpace(payload.UnitNumber))
            {
                return BadRequest(new { message = "Unit number is required." });
            }

            var unit = new PropertyUnit
            {
                PropertyId = propertyId,
                UnitNumber = payload.UnitNumber,
                UnitDescription = payload.UnitDescription ?? string.Empty,
                NetRentableAreaSqM = payload.NetRentableAreaSqM,
                SpecificUnitRentalPrice = payload.SpecificUnitRentalPrice,
                SpecificUnitSalePrice = payload.SpecificUnitSalePrice,
                IsCurrentlyVacant = payload.IsCurrentlyVacant,
                UnitImagesJson = "[]",
                CreatedAt = DateTime.UtcNow
            };

            _context.PropertyUnits.Add(unit);
            await _context.SaveChangesAsync();

            return Ok(unit);
        }

        // --- ❌ ENDPOINT: DELETE SUB-UNIT FROM PROPERTY ---
        [HttpDelete("{propertyId}/units/{unitId}")]
        public async Task<IActionResult> DeletePropertyUnit(int propertyId, int unitId)
        {
            var unit = await _context.PropertyUnits.FirstOrDefaultAsync(u => u.PropertyUnitId == unitId && u.PropertyId == propertyId);
            if (unit == null) return NotFound(new { message = "Property unit not found." });

            _context.PropertyUnits.Remove(unit);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Unit successfully unlinked and deleted from property." });
        }

        // --- 🏠 ENDPOINT: CREATE PROPERTY WITH OPTIONAL SUB-UNITS ---
        [HttpPost("create")]
        public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
            {
                return BadRequest(new { message = "Property address is required." });
            }

            var newProp = new PropertyAsset
            {
                TenantId = "TEN-001",
                CompleteAddress = payload.CompleteAddress,
                ErfSpatialSize = payload.ErfSpatialSize ?? "200 m²",
                TitleDeedType = payload.TitleDeedType ?? "Freehold",
                PortfolioZoning = payload.PortfolioZoning ?? "Commercial Zone",
                MarketClass = payload.MarketClass ?? "Sale",
                PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : 1,
                ValuationPrice = payload.ValuationPrice,
                SalePrice = payload.SalePrice,
                BaseRentalRate = payload.BaseRentalRate,
                RentEscalationPercentage = payload.RentEscalationPercentage,
                LeaseTermMonths = payload.LeaseTermMonths,
                TenantInstallationAllowance = payload.TenantInstallationAllowance,
                Latitude = payload.Latitude != 0 ? payload.Latitude : -33.9249m,
                Longitude = payload.Longitude != 0 ? payload.Longitude : 18.4241m,
                LinkedOwnerId = payload.LinkedOwnerId ?? "OT-0001",
                VerificationDocumentsJson = "[]",
                CreatedAt = DateTime.UtcNow
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
                        IsCurrentlyVacant = unitPayload.IsCurrentlyVacant,
                        UnitImagesJson = "[]",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.PropertyAssets.Add(newProp);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPropertyById), new { propertyId = newProp.PropertyId }, newProp);
        }
        // --- 🔍 ENDPOINT A: MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
        [HttpPost("search")]
        public async Task<IActionResult> SearchProperties([FromBody] PropertySearchCriteria criteria)
        {
            // Eagerly load relational structures (Property Type lookups and sub-units)
            var query = _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .AsQueryable();

            // 1. Filter by target locations array matching text boundaries
            if (criteria.Locations != null && criteria.Locations.Any())
            {
                query = query.Where(p => criteria.Locations.Any(loc => p.CompleteAddress.Contains(loc)));
            }

            // 2. Filter by normalized Property Type lookups
            if (!string.Equals(criteria.PropertyType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.PropertyTypeDetails != null && 
                                         p.PropertyTypeDetails.Name.Equals(criteria.PropertyType, StringComparison.OrdinalIgnoreCase));
            }

            var results = await query.ToListAsync();
            return Ok(results);
        }

        // --- 🖼️ ENDPOINT B: KAFKA-BACKED IMAGE STREAMING UPLOADER ---
        [HttpPost("{propertyId}/upload-image")]
        public async Task<IActionResult> UploadPropertyImage(int propertyId, [FromBody] ImageUploadPayload payload)
        {
            var property = await _context.PropertyAssets.FindAsync(propertyId);
            if (property == null) return NotFound(new { message = "Target property record not found." });

            if (payload == null || string.IsNullOrEmpty(payload.Base64DataString))
            {
                return BadRequest(new { message = "Invalid picture data payload." });
            }

            // Construct transactional message payload envelope
            string messageValue = $"{{\"PropertyId\":{propertyId},\"FileName\":\"{payload.FileName}\",\"Base64Data\":\"{payload.Base64DataString}\"}}";
            
            // Dispatch asynchronously directly onto your Kafka clusters media pipeline
            await _kafkaPublisher.PublishMessageAsync("property-images", propertyId.ToString(), messageValue);

            return Ok(new { message = "Media payload successfully dispatched to Kafka event streams for optimization." });
        }
        // --- 🔗 ENDPOINT C: PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
        [HttpPost("syndicate")]
        public async Task<IActionResult> ToggleSyndication([FromBody] SyndicationTogglePayload payload)
        {
            if (payload == null) return BadRequest(new { message = "Invalid syndication data envelope." });

            var property = await _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .FirstOrDefaultAsync(p => p.PropertyId == payload.PropertyId);

            if (property == null) return NotFound(new { message = "Target property record not found." });

            var platform = await _context.ExternalPlatforms.FindAsync(payload.PlatformId);
            if (platform == null) return NotFound(new { message = "Target external marketing channel portal not found." });

            // Fetch or create the portal links ledger row matching current identifiers
            var syndicationRecord = await _context.PropertyPlatformSyndications
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
                _context.PropertyPlatformSyndications.Add(syndicationRecord);
            }
            else
            {
                syndicationRecord.IsSyndicatedActive = payload.SyndicateStatus;
                syndicationRecord.LastSynchronizedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Dispatch message payload back onto your active Kafka data synchronization topic channels
            string activeState = payload.SyndicateStatus ? "true" : "false";
            string kafkaSyncValue = $"{{\"PropertyId\":{payload.PropertyId},\"PlatformId\":{payload.PlatformId},\"IsActive\":{activeState}}}";
            
            await _kafkaPublisher.PublishMessageAsync("platform-syndication", payload.PropertyId.ToString(), kafkaSyncValue);

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
    }
}
