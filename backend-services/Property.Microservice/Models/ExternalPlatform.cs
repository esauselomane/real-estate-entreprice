using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
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
}
