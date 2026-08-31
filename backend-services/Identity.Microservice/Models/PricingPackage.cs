using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public class PricingPackage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] [MaxLength(100)] public string Code { get; set; } = string.Empty;
        [Required] [MaxLength(200)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? MonthlyPrice { get; set; }
        public decimal? AnnualPrice { get; set; }
        public string? FeaturesJson { get; set; } = "[]";
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
