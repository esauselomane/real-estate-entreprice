using System.ComponentModel.DataAnnotations;

namespace CRM.Microservice.DTOs
{
    public record class BroadcastMessagePayload
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        // Optional role filter (e.g. "Broker", "AgencyAdmin"). Omit = all users in tenant.
        public string? RoleFilter { get; set; }

        public string? LinkType { get; set; }
        public string? LinkId { get; set; }
    }
}
