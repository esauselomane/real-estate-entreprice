using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
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
