using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class AgencyRecord
    {
        [Key]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

         [Required]
        public string Name { get; set; } = string.Empty;

        public string TradingName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string HeadquartersAddress { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = "Active"; // Active, TrialPending, TrialExpired, De-registered
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🧬 HIERARCHICAL AGENCY TREE — null for root agencies; otherwise the parent AgencyId
        public string? ParentAgencyId { get; set; }

        [ForeignKey("ParentAgencyId")]
        public AgencyRecord? ParentAgency { get; set; }

        public ICollection<AgencyRecord> ChildAgencies { get; set; } = new List<AgencyRecord>();

        // 🎯 TRIAL ONBOARDING FIELDS
        public bool IsTrial { get; set; } = false;
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }

        public ICollection<ContactPerson> ContactPersons { get; set; } = new List<ContactPerson>();
    }
}
