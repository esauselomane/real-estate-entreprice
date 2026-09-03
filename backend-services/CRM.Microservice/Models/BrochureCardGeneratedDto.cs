namespace CRM.Microservice.Models
{
    public class BrochureCardGeneratedDto
    {
        public int DealId { get; set; }
        public int CardId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
