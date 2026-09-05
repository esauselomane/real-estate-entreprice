using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class DealBrokerSplit
    {
        [Key]
        public int SplitId { get; set; }

        [Required]
        public int DealId { get; set; }

        [Required]
        public string BrokerId { get; set; } = string.Empty;

        [Required]
        public string BrokerName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SplitPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SplitAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("DealId")]
        public DealRecord? Deal { get; set; }
    }
}
