namespace CRM.Microservice.Models
{
    // 🛠️ Partial-update DTO for `PUT /crm/deal/{id}`. Every field is nullable
    // so the frontend can submit only the fields the user actually changed.
    // Mapping back onto the tracked `DealRecord` is the controller's job.
    public class UpdateDealPayload
    {
        public string? BuyerClientId { get; set; }
        public string? SellerClientId { get; set; }
        public DealWorkflowStage? DealStage { get; set; }
        public decimal? TransactionValue { get; set; }
        public string? DealNotes { get; set; }
        public string? AllocatedBrokerIds { get; set; }
        public string? CommissionType { get; set; }
        public decimal? CommissionValue { get; set; }
        public decimal? RealisedCommission { get; set; }
        public int? LinkedPropertyId { get; set; }
    }
}
