using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
        public record class PropertyMatchDto
        {
            public int PropertyId { get; set; }
            public string? LinkedOwnerId { get; set; }
            public decimal ValuationPrice { get; set; }
        }
}
