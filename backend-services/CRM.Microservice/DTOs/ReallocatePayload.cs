using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
        public record class ReallocatePayload { public int TargetId { get; set; } public bool IsDeal { get; set; } public string TargetAgentName { get; set; } = string.Empty; }
}
