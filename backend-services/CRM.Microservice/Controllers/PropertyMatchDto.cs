namespace CRM.Microservice.Controllers
{
        public class PropertyMatchDto
        {
            public int PropertyId { get; set; }
            public string? LinkedOwnerId { get; set; }
            public decimal ValuationPrice { get; set; }
        }
}
