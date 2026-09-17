using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class PropertyAsset
    {
        [Key]
        public int PropertyId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty; // Sandbox Isolation Key

        [Required]
        public string CompleteAddress { get; set; } = string.Empty;

        [Required]
        public string ErfSpatialSize { get; set; } = string.Empty;

        [Required]
        public string TitleDeedType { get; set; } = string.Empty;

        [Required]
        public string PortfolioZoning { get; set; } = string.Empty;

        [Required]
        // Explicit Listing Classification: "Sale" or "Rental" (legacy single-value; superseded by flags below)
        public string MarketClass { get; set; } = "Sale";

        // 📈 Listing eligibility — a single asset may be marketed for SALE and/or RENT at the same time.
        public bool IsForSale { get; set; } = true;
        public bool IsForRent { get; set; } = false;

        [Required]
        public int PropertyTypeId { get; set; } // Foreign Key link to lookup table

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValuationPrice { get; set; } // Global historical appraisal fallback

        // --- 💰 FORKED FINANCIAL TRANSACTION MATRIX ENTRIES ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; } // Populated if MarketClass == "Sale"

        // --- 🏗️ TENANT INCENTIVES & ALLOWANCES ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal TenantInstallationAllowance { get; set; } // Numerical incentive budget allocation

        // --- 📍 SPATIAL GEO-COORDINATE PLACEMENTS ---
        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        [Required]
        public string VerificationDocumentsJson { get; set; } = "[]";

        [Required]
        public string VerificationDocumentMetadataJson { get; set; } = "[]";

        [Required]
        public string GalleryImageJson { get; set; } = "[]";

        [Required]
        public string LinkedOwnerId { get; set; } = string.Empty;

        [Required]
        public string PropertyDescription { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- 🔗 RELATIONAL STRUCTURAL ANCHOR NAVIGATION PROPERTIES ---
        [ForeignKey("PropertyTypeId")]
        public PropertyType? PropertyTypeDetails { get; set; }

        // Master-Detail tracking linking to multi-tenant units inside this property container
        public ICollection<PropertyUnit> SubUnits { get; set; } = new List<PropertyUnit>();

        // 📨 RESTORED: Allows the system to read all active marketing channels this specific asset is pushed to
        public ICollection<PropertyPlatformSyndication> SyndicationLinks { get; set; } = new List<PropertyPlatformSyndication>();

        // 📜 Sole-mandate history. One PropertyAsset → many SoleMandates,
        // but the DB enforces at-most-one active via a filtered unique
        // index (see Program.cs migration).
        public ICollection<SoleMandate> SoleMandates { get; set; } = new List<SoleMandate>();
    }
}
