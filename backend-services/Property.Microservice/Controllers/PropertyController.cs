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
