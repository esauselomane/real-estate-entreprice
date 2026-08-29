using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class AgencyRecord
    {
        [Key]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

         [Required]
        public string Name { get; set; } = string.Empty;

        public string TradingName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string HeadquartersAddress { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ContactPerson> ContactPersons { get; set; } = new List<ContactPerson>();
    }
}
