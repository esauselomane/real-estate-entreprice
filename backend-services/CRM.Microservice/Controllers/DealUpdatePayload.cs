namespace CRM.Microservice.Controllers
{
        public class DealUpdatePayload { public int DealId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; }
}
