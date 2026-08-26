using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    // 🏷️ 1. NORMALIZED PROPERTY TYPES REFERENCE LOOKUP TABLE
    public class PropertyType
    {
        [Key]
        public int PropertyTypeId { get; set; }

        [Required]
        [MaxLength(50)] // e.g., Industrial, Retail, Residential, Commercial
        public string Name { get; set; } = string.Empty;
    }

    // 🏠 2. EXPANDED MASTER PROPERTY INVENTORY ENTITY
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
        // Explicit Listing Classification: "Sale" or "Rental"
        public string MarketClass { get; set; } = "Sale"; 
        [Required]
        public int PropertyTypeId { get; set; } // Foreign Key link to lookup table

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValuationPrice { get; set; } // Global historical appraisal fallback

        // --- 💰 FORKED FINANCIAL TRANSACTION MATRIX ENTRIES ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; } // Populated if MarketClass == "Sale"

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BaseRentalRate { get; set; } // Populated if MarketClass == "Rental"

        [Column(TypeName = "decimal(5,2)")]
        public decimal? RentEscalationPercentage { get; set; } // Annual lease compound markup e.g. 7.50%

        public int? LeaseTermMonths { get; set; } // Expected contract commitment length

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
        public string LinkedOwnerId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- 🔗 RELATIONAL STRUCTURAL ANCHOR NAVIGATION PROPERTIES ---
        [ForeignKey("PropertyTypeId")]
        public PropertyType? PropertyTypeDetails { get; set; }

        // Master-Detail tracking linking to multi-tenant units inside this property container
        public ICollection<PropertyUnit> SubUnits { get; set; } = new List<PropertyUnit>();

        // 📨 RESTORED: Allows the system to read all active marketing channels this specific asset is pushed to
        public ICollection<PropertyPlatformSyndication> SyndicationLinks { get; set; } = new List<PropertyPlatformSyndication>();
    }

    // 🏢 3. GRANULAR MULTI-TENANT PROPERTY UNIT DETAILS SUB-TABLE
    public class PropertyUnit
    {
        [Key]
        public int PropertyUnitId { get; set; }

        [Required]
        public int PropertyId { get; set; } // Master Foreign Key link

        [Required]
        [MaxLength(20)] // e.g., "Suite 101", "Bay A"
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

        [Required]
        public string UnitImagesJson { get; set; } = "[]"; // Serialized string array tracking individual room pictures

        public bool IsCurrentlyVacant { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- 🔗 NAVIGATION ANCHOR BACK TO MASTER ASSET PROPERTY ---
        [ForeignKey("PropertyId")]
        [System.Text.Json.Serialization.JsonIgnore]
        public PropertyAsset? MasterPropertyDetails { get; set; }
    }
    // 📊 4. BROCHURE & QUOTE PRESENTATION PAYLOAD DATA ENTITY
    public class GeneratedBrochurePayload
    {
        [Required]
        public int DealId { get; set; } // Tracks target CRM baseline presentation workflow trigger

        [Required]
        public int PropertyId { get; set; } // Binds layout details to master spatial records

        [Required]
        public string BrokerName { get; set; } = string.Empty;

        [Required]
        public string BrokerEmail { get; set; } = string.Empty;

        [Required]
        public string BrokerPhone { get; set; } = string.Empty;

        [Required]
        public string CompanyReferenceNumber { get; set; } = string.Empty;

        // Serialized string array storing the specific file choices selected by the agent (limited up to 5 maximum constraints)
        [Required]
        public string SelectedImagesJson { get; set; } = "[]"; 

        public DateTime LayoutGeneratedAt { get; set; } = DateTime.UtcNow;
    }

        // 📨 5. KAFKA SYNDICATION PORTALS DIRECTORY LOOKUP TABLE (Restored)
    public class ExternalPlatform
    {
        [Key]
        public int PlatformId { get; set; }

        [Required]
        [MaxLength(100)] // e.g., Property24, PrivateProperty, Gumtree
        public string PlatformName { get; set; } = string.Empty;

        [Required]
        public string BaseUrlAddress { get; set; } = string.Empty;
    }

    // 🔗 6. MULTI-TENANT EXTERNAL PORTAL SYNDICATION LINKS LEDGER (Restored)
    public class PropertyPlatformSyndication
    {
        [Key]
        public int SyndicationId { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int PlatformId { get; set; }

        public bool IsSyndicatedActive { get; set; } = false;
        public string ExternalReferenceIndex { get; set; } = string.Empty; // External portal unique tracking ID
        public string LivePortalListingUrl { get; set; } = string.Empty; // e.g., Link directly to Property24 ad sheet
        public DateTime LastSynchronizedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PropertyId")]
        [System.Text.Json.Serialization.JsonIgnore]
        public PropertyAsset? PropertyDetails { get; set; }

        [ForeignKey("PlatformId")]
        public ExternalPlatform? PlatformDetails { get; set; }
    }

}
