namespace Property.Microservice.Controllers
{
    public class MaintenanceCallPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }
}
