namespace CRM.Microservice.Controllers
{
    public class DealBrokerSplitDto
    {
        public int SplitId { get; set; }
        public int DealId { get; set; }
        public string BrokerId { get; set; } = string.Empty;
        public string BrokerName { get; set; } = string.Empty;
        public decimal SplitPercentage { get; set; }
        public decimal SplitAmount { get; set; }
    }

    public class UpsertBrokerSplitsRequest
    {
        public int DealId { get; set; }
        public List<DealBrokerSplitDto> Splits { get; set; } = new();
    }
}
