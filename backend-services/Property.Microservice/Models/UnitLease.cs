using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
    public class UnitLease
    {
        [Key]
        public int UnitLeaseId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;        // tenant user id (NameIdentifier)

        [Required]
        public string TenantId { get; set; } = string.Empty;       // tenant sandbox

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int PropertyUnitId { get; set; }

        public DateTime LeaseStart { get; set; }
        public DateTime LeaseEnd { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🧑 Tenant (CRM contact) linkage. Replaces the old free-text "ContactName"
        // on the operational record ledger. Nullable so historical leases can be
        // backfilled without a contact.
        public string? ContactId { get; set; }

        // 💰 Financial snapshot stored on the lease at the time of agreement. The
        // live rental rate on the unit (PropertyUnits.RentalRate) remains the
        // authoritative rent, this is the historical record.
        public decimal? MonthlyRent { get; set; }
        public decimal? DepositAmount { get; set; }

        // 📝 Free-form notes (notice periods, special conditions, escalation notes).
        public string? Notes { get; set; }

        // 💰 Interest accrued on the tenant's deposit, expressed as a percentage
        // PER YEAR (e.g. 5.00 = 5% p.a.). Combined with the lease term on the
        // read path to surface "Amount due to tenant on lease termination" =
        // Deposit × (1 + (AccruedInterestPercent / 100) × (months / 12)).
        public decimal? AccruedInterestPercent { get; set; }

        // 🏷️ Snapshotted tenant full name at lease agreement. Mirrors the
        // financial-snapshot pattern used for MonthlyRent/DepositAmount so
        // the history list can render "John Smith (CON-XXX)" even if the
        // underlying CRMContact is later renamed or deleted.
        [MaxLength(200)]
        public string? TenantNameSnapshot { get; set; }
    }
}
