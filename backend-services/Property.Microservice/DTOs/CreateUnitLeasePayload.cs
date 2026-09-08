using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
    public record class CreateUnitLeasePayload
    {
        // 🆔 The tenant user the lease is for. Optional — a lease can be created
        // before the tenant user is assigned (e.g. vacant unit, awaiting tenant).
        public string? UserId { get; set; }

        [Required]
        public DateTime LeaseStart { get; set; }

        [Required]
        public DateTime LeaseEnd { get; set; }

        public string? ContactId { get; set; }
        public decimal? MonthlyRent { get; set; }
        public decimal? DepositAmount { get; set; }
        public string? Notes { get; set; }
        // % p.a. interest on the deposit. Used to compute the read-only
        // "Amount due to tenant on lease termination" on the frontend.
        public decimal? AccruedInterestPercent { get; set; }
        // Optional — the frontend typically sends the name of the picked
        // CRM contact alongside contactId. The controller will auto-fill
        // this from the CRM if omitted, so historical re-imports work too.
        public string? TenantNameSnapshot { get; set; }
    }
}
