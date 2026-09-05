using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Property.Microservice.Controllers
{
    // 📚 SuperAdmin-managed amenity catalogue. Lets the platform owner add or
    // retire amenity types (e.g. "Parking", "PetsAllowed", "Gym") so the
    // dropdown on the property/amenity editor stays data-driven.
    // Route is nested under /api/properties so the gateway's existing property
    // re-route picks it up with no config change.
    [ApiController]
    [Route("api/properties")]
    public class AmenityTypeController : ControllerBase
    {
        private readonly PropertyDbContext _context;

        public AmenityTypeController(PropertyDbContext context)
        {
            _context = context;
        }

        [HttpGet("amenity-types")]
        public async Task<IActionResult> GetAmenityTypes()
        {
            var types = await _context.AmenityTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.AmenityTypeId)
                .Select(t => new
                {
                    t.AmenityTypeId,
                    t.Name,
                    t.Label,
                    t.RequiresBoolean,
                    t.RequiresNumber,
                    t.IsResidentialOnly,
                    t.IsActive
                })
                .ToListAsync();
            return Ok(types);
        }

        // 📋 Admin form: list ALL (incl. inactive) so the catalogue editor can
        // toggle retired types back on.
        [HttpGet("amenity-types/all")]
        [Authorize(Roles = "PlatformAdmin,AgencyAdmin")]
        public async Task<IActionResult> GetAllAmenityTypes()
        {
            var types = await _context.AmenityTypes.OrderBy(t => t.AmenityTypeId)
                .Select(t => new
                {
                    t.AmenityTypeId,
                    t.Name,
                    t.Label,
                    t.RequiresBoolean,
                    t.RequiresNumber,
                    t.IsResidentialOnly,
                    t.IsActive
                })
                .ToListAsync();
            return Ok(types);
        }

        [HttpPost("amenity-types")]
        [Authorize(Roles = "PlatformAdmin,AgencyAdmin")]
        public async Task<IActionResult> CreateAmenityType([FromBody] AmenityTypeDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "A name is required." });
            if (!string.Equals(dto.Name, dto.Name?.Trim(), StringComparison.Ordinal))
                return BadRequest(new { message = "Name must not have surrounding whitespace." });
            if (await _context.AmenityTypes.AnyAsync(t => t.Name == dto.Name))
                return Conflict(new { message = "An amenity with that name already exists." });

            var entity = new AmenityType
            {
                Name = dto.Name.Trim(),
                Label = string.IsNullOrWhiteSpace(dto.Label) ? dto.Name.Trim() : dto.Label.Trim(),
                RequiresBoolean = dto.RequiresBoolean,
                RequiresNumber = dto.RequiresNumber,
                IsResidentialOnly = dto.IsResidentialOnly,
                IsActive = dto.IsActive
            };
            _context.AmenityTypes.Add(entity);
            await _context.SaveChangesAsync();
            return Created($"/api/properties/amenity-types/{entity.AmenityTypeId}", entity);
        }

        [HttpPut("amenity-types/{id:int}")]
        [Authorize(Roles = "PlatformAdmin,AgencyAdmin")]
        public async Task<IActionResult> UpdateAmenityType(int id, [FromBody] AmenityTypeDto dto)
        {
            var entity = await _context.AmenityTypes.FirstOrDefaultAsync(t => t.AmenityTypeId == id);
            if (entity == null) return NotFound(new { message = "Amenity type not found." });

            // Name uniqueness (excluding self). Avoid the null-conditional operator
            // inside the EF expression tree — it's illegal there.
            var newName = (dto.Name ?? string.Empty).Trim();
            if (!string.Equals(entity.Name, newName, StringComparison.Ordinal)
                && await _context.AmenityTypes.AnyAsync(t => t.AmenityTypeId != id && t.Name == newName))
                return Conflict(new { message = "An amenity with that name already exists." });

            entity.Name = dto.Name?.Trim() ?? entity.Name;
            entity.Label = string.IsNullOrWhiteSpace(dto.Label) ? dto.Name?.Trim() ?? entity.Name : dto.Label.Trim();
            entity.RequiresBoolean = dto.RequiresBoolean;
            entity.RequiresNumber = dto.RequiresNumber;
            entity.IsResidentialOnly = dto.IsResidentialOnly;
            entity.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Amenity type updated." });
        }
    }

    public class AmenityTypeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool RequiresBoolean { get; set; }
        public bool RequiresNumber { get; set; }
        public bool IsResidentialOnly { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // Used by UpsertPropertyAmenity. The amenity type is chosen by id; only the
    // value fields the type's flags request get persisted.
    public class AmenityUpsertDto
    {
        public int AmenityTypeId { get; set; }
        public bool? BooleanValue { get; set; }
        public int? NumberValue { get; set; }
        public string? Notes { get; set; }
    }
}
