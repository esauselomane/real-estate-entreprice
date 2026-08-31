using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    // 📋 Public-facing lead/intake requests captured from the website (Bibsy + pricing).
    // These are NOT tenant/users — they live in their own table and are only readable
    // by the SuperAdmin role until a broker converts them into a real agency.
    public enum LeadUserType
    {
        Agency,
        Individual
    }

    public enum LeadPackage
    {
        SMEAgency,
        Private,
        Enterprise
    }

    public enum LeadStatus
    {
        New,           // awaiting first contact
        Contacted,     // a broker called the prospect
        QuoteSent,     // a quote was issued
        Paid,          // payment confirmed — eligible for agency provisioning
        AgencyCreated  // the real TenantAgency was provisioned
    }

    public class LeadCaptureRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public string FullName { get; set; } = string.Empty;
        [Required] public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Company { get; set; }

        [Required] public LeadUserType UserType { get; set; } = LeadUserType.Individual;
        public LeadPackage? Package { get; set; }
        public bool DemoRequested { get; set; } = false;
        public string? Message { get; set; }
        public string? Source { get; set; } = "website"; // website | pricing

        public LeadStatus Status { get; set; } = LeadStatus.New;
        public string? HandledBy { get; set; }         // SuperAdmin who actioned it
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
