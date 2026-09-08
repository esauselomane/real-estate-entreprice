using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
    public record class BrochureCardGeneratedDto
    {
        public int DealId { get; set; }
        public int CardId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
