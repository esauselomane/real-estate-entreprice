namespace CRM.Microservice.DTOs
{
    public record BrokerConversionRateDto
    {
        public string BrokerId { get; init; } = string.Empty;
        public string BrokerName { get; init; } = string.Empty;
        public int TotalLeads { get; init; }
        public int DealsWon { get; init; }
        public decimal ConversionRate { get; init; }
        public decimal AvgDaysToClose { get; init; } = 0m;
        public decimal AvgDealValue { get; init; } = 0m;
        public decimal AvgCommissionGenerated { get; init; } = 0m;
    }

    public record BrokerPerformanceDto
    {
        public string BrokerId { get; init; } = string.Empty;
        public string BrokerName { get; init; } = string.Empty;
        public decimal LeadToDealConversionRate { get; init; }
        public decimal AvgDaysToClose { get; init; }
        public decimal AvgDealValue { get; init; }
        public decimal AvgCommissionGenerated { get; init; }
        public decimal StageVelocityScore { get; init; }
        public decimal GeoSpecializationScore { get; init; }
        public Dictionary<string, decimal> PropertyTypeScore { get; init; } = new();
        public Dictionary<string, decimal> PriceBandScore { get; init; } = new();
        public decimal CrmHygieneScore { get; init; }
        public decimal OverallScore { get; init; }
        public int TotalLeadsAssigned { get; init; }
        public int TotalDealsWon { get; init; }
        public string ScoreLabel { get; init; } = string.Empty;
    }

    public record SuggestedBrokerDto
    {
        public string BrokerId { get; init; } = string.Empty;
        public string BrokerName { get; init; } = string.Empty;
        public decimal MatchScore { get; init; }
        public string Label { get; init; } = string.Empty;
        public decimal ConversionRate { get; init; }
        public decimal AvgDaysToClose { get; init; }
        public decimal AvgDealValue { get; init; }
        public Dictionary<string, decimal> GeoSpecialization { get; init; } = new();
    }

    public record SuggestedBrokersResponse
    {
        public List<SuggestedBrokerDto> Brokers { get; init; } = new();
    }
}
