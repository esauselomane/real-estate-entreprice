namespace CRM.Microservice.DTOs
{
    public record AgencyGlanceSummary
    {
        public int TotalUsers { get; init; }
        public int TotalBrokers { get; init; }
        public int TotalLeads { get; init; }
        public int TotalDeals { get; init; }
        public int TotalPropertiesWithSoleMandates { get; init; }
        public decimal TotalRealisedCommission { get; init; }
        public int ActiveListings { get; init; }
        public int ClosedDeals { get; init; }
    }

    public record AgencyGlanceTabData
    {
        public AgencyDetailsDto AgencyDetails { get; init; } = new();
        public List<ContactPersonDto> ContactPersons { get; init; } = new();
        public List<SubscriptionPackageDto> ActiveSubscriptions { get; init; } = new();
        public List<SubscriptionPackageDto> PastSubscriptions { get; init; } = new();
        public List<UserGrowthPoint> UsersOverTime { get; init; } = new();
        public List<PropertyGrowthPoint> PropertiesOverTime { get; init; } = new();
    }

    public record AgencyDetailsDto
    {
        public string AgencyId { get; init; } = string.Empty;
        public string TenantId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string TradingName { get; init; } = string.Empty;
        public string RegistrationNumber { get; init; } = string.Empty;
        public string ContactEmail { get; init; } = string.Empty;
        public string HeadquartersAddress { get; init; } = string.Empty;
        public string OperationalStatus { get; init; } = string.Empty;
        public string BrokerageLicense { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    public record ContactPersonDto
    {
        public string FullName { get; init; } = string.Empty;
        public string EmailAddress { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string PositionJobTitle { get; init; } = string.Empty;
        public string ContactType { get; init; } = string.Empty;
        public bool IsAgencyAdmin { get; init; }
    }

    public record SubscriptionPackageDto
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal? MonthlyPrice { get; init; }
        public decimal? AnnualPrice { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public record UserGrowthPoint
    {
        public string Month { get; init; } = string.Empty;
        public int UserCount { get; init; }
    }

    public record PropertyGrowthPoint
    {
        public string Month { get; init; } = string.Empty;
        public int PropertyCount { get; init; }
    }
}
