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

        // 🧑‍💼 Multi-broker assignment: comma-separated broker ids. Resolved at
        // read time so renames flow through. Legacy single-agent field is gone.
        [Required]
        public string AllocatedBrokerIds { get; set; } = string.Empty;

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

        // 💱 True when RealisedCommission was filled by the server formula
        // (CommissionType × CommissionValue/TransactionValue) because the
        // user left it blank/zero. Lets the UI render an "AUTO" badge so
        // editors can tell what was hand-entered vs derived.
        public bool RealisedCommissionIsAuto { get; set; } = false;

        public ICollection<DealBrokerSplit> BrokerSplits { get; set; } = new List<DealBrokerSplit>();
    }
}
