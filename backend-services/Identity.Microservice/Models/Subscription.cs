using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public class Subscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PeachSubscriptionId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PeachPriceId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string PricingPackageCode { get; set; } = string.Empty;

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

        public DateTime CurrentPeriodStart { get; set; }

        public DateTime CurrentPeriodEnd { get; set; }

        public DateTime? TrialEndsAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TenantId))]
        public virtual TenantAgency TenantAgency { get; set; } = null!;
    }

    public enum SubscriptionStatus
    {
        Trialing = 0,
        Active = 1,
        PastDue = 2,
        Cancelled = 3,
        Expired = 4,
        Paused = 5
    }
}