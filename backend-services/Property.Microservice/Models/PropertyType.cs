using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
    public class PropertyType
    {
        [Key]
        public int PropertyTypeId { get; set; }

        [Required]
        [MaxLength(50)] // e.g., Industrial, Retail, Residential, Commercial
        public string Name { get; set; } = string.Empty;
    }
}
