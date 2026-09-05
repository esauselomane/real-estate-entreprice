namespace CRM.Microservice.Controllers
{
        public class ReallocatePayload { public int TargetId { get; set; } public bool IsDeal { get; set; } public string TargetAgentName { get; set; } = string.Empty; }
}
