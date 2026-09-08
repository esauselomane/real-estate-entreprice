using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
    public record class TrialOnboardingRequest
    {
        public string AgencyName { get; set; } = string.Empty;
        public string TradingName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string HeadquartersAddress { get; set; } = string.Empty;

        public string AdminFullName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPhone { get; set; } = string.Empty;
    }
}
