using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
    // 📚 Catalogue of amenity types an agency can attach to a property or a unit.
    // Managed from the SuperAdmin workspace so new amenities can be added
    // without a code deploy. The Boolean/Number flags drive the UI field that
    // is rendered (toggle vs number input). IsResidentialOnly gates whether
    // the amenity is permitted on a unit at all for residential assets.
    public class AmenityType
    {
        [Key]
        public int AmenityTypeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // e.g. "Parking", "PetsAllowed"

        // When true the amenity is rendered as a toggle (pets allowed).
        // When false and RequiresNumber is true it's a number field (parking bays).
        public bool RequiresBoolean { get; set; } = false;
        public bool RequiresNumber { get; set; } = false;

        // true => only attachable to a property whose PropertyType.Name == "Residential"
        public bool IsResidentialOnly { get; set; } = false;

        [MaxLength(100)]
        public string? Label { get; set; } // Friendly label shown in the UI

        public bool IsActive { get; set; } = true;
    }
}
