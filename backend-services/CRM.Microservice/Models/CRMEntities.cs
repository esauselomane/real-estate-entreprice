using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    // 🏢 1. THE PARENT SYSTEM COMPANY PROFILE MODEL (Editable)
    public class CRMCompany
    {
        [Key]
        public string CompanyId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string BusinessName { get; set; } = string.Empty;

        public string RegistrationNumber { get; set; } = string.Empty;
        public string VatNumber { get; set; } = string.Empty;
        public string CorporateWebsite { get; set; } = string.Empty;
        public string HeadOfficeAddress { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CorporateCreditLimit { get; set; }

        public string IndustryVertical { get; set; } = "Real Estate Investment";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AgencyRecord
    {
        [Key]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string ContactEmail { get; set; } = string.Empty;
        public string HeadquartersAddress { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BrokerRecord
    {
        [Key]
        public string BrokerId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = "Broker";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class OwnerTenantRecord
    {
        [Key]
        public int OwnerTenantId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string RecordType { get; set; } = "Owner";

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PrimaryLocation { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetLimit { get; set; }

        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ⚖️ 2. THE CONTRACTUAL LEGAL ENTITY PROFILE MODEL (Editable)
    public class CRMLegalEntity
    {
        [Key]
        public string LegalEntityId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string RegisteredLegalName { get; set; } = string.Empty;

        public string AssociatedCompanyId { get; set; } = string.Empty;

        [Required]
        public string EntityTypeClassifier { get; set; } = "Private Company";

        public string TaxRegistrationIndex { get; set; } = string.Empty;
        public string DeedsOfficeIdentifier { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AssociatedCompanyId")]
        public CRMCompany? CompanyDetails { get; set; }
    }

    // 👤 3. THE HUMAN CONTACT PROFILE MODEL (Editable)
    public class CRMContact
    {
        [Key]
        public string ContactId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string PositionJobTitle { get; set; } = "Director";
        public string ContactRoleType { get; set; } = "Buyer";

        public string AssociatedCompanyId { get; set; } = string.Empty;
        public string AssociatedLegalEntityId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AssociatedCompanyId")]
        public CRMCompany? CompanyDetails { get; set; }

        [ForeignKey("AssociatedLegalEntityId")]
        public CRMLegalEntity? LegalEntityDetails { get; set; }
    }

    public enum LeadWorkflowStage
    {
        Contacted,
        AttemptedContact,
        Nurturing,
        Disqualified,
        Qualified
    }

    public enum DealWorkflowStage
    {
        DiscoveryNeedsAnalysis,
        Presentation,
        ProposalQuoteSent,
        Negotiation,
        ClosedWon,
        ClosedLost
    }

    public class LeadRecord
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string ClientName { get; set; } = string.Empty;

        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;

        [Required]
        public string AllocatedToAgent { get; set; } = string.Empty;

        [Required]
        public LeadWorkflowStage LeadStage { get; set; } = LeadWorkflowStage.Contacted;

        [Required]
        public string PropertyInterestClass { get; set; } = string.Empty;

        [Required]
        public string PreferredArea { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedBudget { get; set; }

        public string LeadNotes { get; set; } = string.Empty;
        public bool IsConvertedToDeal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

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
    }

    public class PipelineSummaryReport
    {
        public int ActiveLeadsCount { get; set; }
        public int ValuationsInProcessCount { get; set; }
        public int ConveyancingAtDeedsCount { get; set; }
        public int FinalizedClosedCount { get; set; }
        public decimal TotalPipelineEstimatedValue { get; set; }
    }

    public class ReportDrilldownRow
    {
        public string Period { get; set; } = string.Empty;
        public int Leads { get; set; }
        public int Deals { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CRMReportOverview
    {
        public int TotalLeads { get; set; }
        public int TotalDeals { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<ReportDrilldownRow> MonthlyBreakdown { get; set; } = new();
    }
}
