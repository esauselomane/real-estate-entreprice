using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
    public record class MaintenanceCallPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }
}
