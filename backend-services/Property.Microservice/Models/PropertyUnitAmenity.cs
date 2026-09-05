using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    // 🏠/🏢 A single amenity attached to either a property or a property unit.
    // The discriminator is PropertyUnitId:
    //   NULL  -> property-level amenity (default scope for residential assets
    //            and for amenities that only ever apply to the whole property)
    //   set   -> unit-level amenity (commercial/industrial/retail assets whose
    //            amenities split per unit)
    // A property can have many; a unit can have many. The composite unique
    // index on (PropertyId, PropertyUnitId, AmenityTypeId) enforces one
    // instance of a given amenity per scope.
    public class PropertyUnitAmenity
    {
        [Key]
        public int AmenityId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        // Anchor parent — always required, even for unit-level amenities.
        [Required]
        public int PropertyId { get; set; }

        // Nullable discriminator. NULL = property-level.
        public int? PropertyUnitId { get; set; }

        [Required]
        public int AmenityTypeId { get; set; }

        // Value payload. Only one of these should be populated depending on
        // the amenity type's flags.
        public bool? BooleanValue { get; set; }   // pets allowed: true/false
        public int? NumberValue { get; set; }    // parking bays: N

        // Optional free-text note (e.g. "Bay A reserved for visitor parking")
        [MaxLength(250)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- navigation ---
        [ForeignKey("PropertyId")]
        public PropertyAsset? Property { get; set; }

        [ForeignKey("PropertyUnitId")]
        public PropertyUnit? PropertyUnit { get; set; }

        [ForeignKey("AmenityTypeId")]
        public AmenityType? AmenityType { get; set; }
    }
}
