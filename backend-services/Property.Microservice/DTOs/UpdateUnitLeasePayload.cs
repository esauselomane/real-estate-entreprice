using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
    public record class UpdateUnitLeasePayload
    {
        public DateTime? LeaseStart { get; set; }
        public DateTime? LeaseEnd { get; set; }
        public string? ContactId { get; set; }
        public decimal? MonthlyRent { get; set; }
        public decimal? DepositAmount { get; set; }
        public string? Notes { get; set; }
        // The caller may explicitly end a lease (set IsActive = false) without
        // editing the EndDate; this prevents stale EndDates from being edited
        // after the fact.
        public bool? EndNow { get; set; }
        public decimal? AccruedInterestPercent { get; set; }
        // Snapshot semantics: passing a non-null value updates the stored
        // name; passing null leaves the existing snapshot alone.
        public string? TenantNameSnapshot { get; set; }
    }
}
