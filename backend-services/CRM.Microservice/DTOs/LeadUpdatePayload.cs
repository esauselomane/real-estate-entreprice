using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
        public record class LeadUpdatePayload { public int LeadId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; public int? LinkToPropertyId { get; set; } public string SellerNameIfNewDeal { get; set; } = string.Empty; public string CommissionType { get; set; } = CommissionTypes.Percent; public decimal CommissionValue { get; set; } = 0m; }
}
