using System.ComponentModel.DataAnnotations;

namespace CRM.Microservice.Models
{
    public class CreateNotificationPayload
    {
        // Recipient user id (e.g. USR-XXX). Omit for a tenant-wide broadcast.
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = "system";

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;
        public string LinkType { get; set; } = string.Empty;
        public string LinkId { get; set; } = string.Empty;
    }
}
