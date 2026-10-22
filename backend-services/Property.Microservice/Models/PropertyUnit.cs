using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class PropertyUnit
    {
        [Key]
        public int PropertyUnitId { get; set; }

        [Required]
        public int PropertyId { get; set; } // Master Foreign Key link

        [Required]
        [MaxLength(50)] // e.g., "Suite 101", "Bay A", "Retail Shop 12B"
        public string UnitNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)] // e.g., "Ground Floor Offices", "Cold Storage Warehouse"
        public string UnitDescription { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal NetRentableAreaSqM { get; set; } // NRA m² size calculation

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SpecificUnitRentalPrice { get; set; } // Override value if priced per single unit pocket

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SpecificUnitSalePrice { get; set; } // Override value if units sold individually

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RentalRate { get; set; } // Monthly rental rate for this unit

        [Column(TypeName = "decimal(5,2)")]
        public decimal? RentEscalationPercentage { get; set; } // Annual lease compound markup e.g. 7.50%

        public int? LeaseTermMonths { get; set; } // Expected contract commitment length

        [Required]
        public string UnitImagesJson { get; set; } = "[]"; // Serialized string array tracking individual room pictures

        // 📎 Unit-level document vault metadata. Each entry is
        // { name, description, fileName, storedName, contentType, sizeBytes, uploadedAt }
        // The actual file is persisted to disk under PropertyMediaVault (see UploadPropertyUnitDocument).
        [Required]
        public string UnitDocumentsJson { get; set; } = "[]";

        /// <summary>Public asking deposit for a new letting; independent of existing tenants' lease deposits.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999999999d)]
        public decimal? AdvertisedDepositAmount { get; set; }

        public bool IsCurrentlyVacant { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- 🔗 NAVIGATION ANCHOR BACK TO MASTER ASSET PROPERTY ---
        [ForeignKey("PropertyId")]
        [System.Text.Json.Serialization.JsonIgnore]
        public PropertyAsset? MasterPropertyDetails { get; set; }
    }
}
