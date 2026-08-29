using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class GeographyRecord
    {
        [Key]
        public int GeographyId { get; set; }

        [Required]
        public string Country { get; set; } = "South Africa";

        public string Province { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? Suburb { get; set; }

        [Column(TypeName = "decimal(10,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(10,6)")]
        public decimal Longitude { get; set; }
    }
}
