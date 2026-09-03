using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class DealRecord
    {
        [Key]
        public int DealId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string AllocatedToAgent { get; set; } = string.Empty;

        [Required]
        public DealWorkflowStage DealStage { get; set; } = DealWorkflowStage.DiscoveryNeedsAnalysis;

        public int? LinkedPropertyId { get; set; }

        [Required]
        public string BuyerClientId { get; set; } = string.Empty;

        [Required]
        public string SellerClientId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TransactionValue { get; set; }

        public string DealNotes { get; set; } = string.Empty;
        public DateTime LastStageUpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 💰 Commission locked at promotion time; only realised into revenue when the deal is ClosedWon.
        public string CommissionType { get; set; } = CommissionTypes.Percent;
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionValue { get; set; } = 0m;
        [Column(TypeName = "decimal(18,2)")]
        public decimal? RealisedCommission { get; set; } = null;

        public ICollection<DealBrokerSplit> BrokerSplits { get; set; } = new List<DealBrokerSplit>();
    }
}
