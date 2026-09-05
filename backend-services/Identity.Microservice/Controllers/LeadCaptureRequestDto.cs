namespace Identity.Microservice.Controllers
{
    // 🟢 Public, unauthenticated DTO — sent from the marketing website (Bibsy / pricing).
    public class LeadCaptureRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string UserType { get; set; } = "Individual";   // Agency | Individual
        public string? Package { get; set; }                   // SMEAgency | Private | Enterprise
        public bool DemoRequested { get; set; } = false;
        public string? Message { get; set; }
        public string? Source { get; set; } = "website";
    }
}
