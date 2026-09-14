using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public class PaymentIntent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PeachPaymentId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PeachCheckoutId { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "ZAR";

        public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Pending;

        [Required]
        [MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? MetadataJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }

    public enum PaymentIntentStatus
    {
        Pending = 0,
        Processing = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5
    }
}