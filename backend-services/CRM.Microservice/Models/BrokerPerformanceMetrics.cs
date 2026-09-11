using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class BrokerPerformanceMetrics
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string BrokerId { get; set; } = string.Empty;

        public DateTime MetricDate { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal LeadToDealConversionRate { get; set; }
        [Column(TypeName = "decimal(7,1)")]
        public decimal AvgDaysToClose { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AvgDealValue { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AvgCommissionGenerated { get; set; }
        [Column(TypeName = "decimal(7,2)")]
        public decimal StageVelocityScore { get; set; }
        [Column(TypeName = "decimal(7,2)")]
        public decimal GeoSpecializationScore { get; set; }
        public string PropertyTypeConversionScore { get; set; } = "{}";
        public string PriceBandScore { get; set; } = "{}";
        [Column(TypeName = "decimal(7,2)")]
        public decimal CrmHygieneScore { get; set; }
        [Column(TypeName = "decimal(7,2)")]
        public decimal OverallScore { get; set; }
        public int TotalLeadsAssigned { get; set; }
        public int TotalDealsWon { get; set; }

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}
