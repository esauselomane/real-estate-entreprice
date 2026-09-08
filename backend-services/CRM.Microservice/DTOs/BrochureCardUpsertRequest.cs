using System.ComponentModel.DataAnnotations;
using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
    public record class BrochureCardUpsertRequest
    {
        public int? CardId { get; set; }
        [Required] public int DealId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;
        public List<BrochureCardLineDto> Units { get; set; } = new();
    }
}
