using System.ComponentModel.DataAnnotations;

namespace CRM.Microservice.Models
{
    // 🛎️ Per-broker (or per-user) notification. Surface in the bell on
    // the broker dashboard. Created when an admin clicks "Remind Agent"
    // on a compliance item, or for system events the broker must see.
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        // Recipient user id (e.g. USR-XXX). If empty the notification is
        // treated as tenant-broadcast (shown to every user in the tenant).
        public string UserId { get; set; } = string.Empty;

        // Free-form category so the bell can group by icon.
        // 'compliance.expiring_mandate' | 'compliance.awaiting_signature' |
        // 'reminder' | 'system'
        [Required]
        public string Category { get; set; } = "system";

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        // Optional deep-link target: dealId | propertyId | etc. so the
        // bell can navigate the user to the right place.
        public string LinkType { get; set; } = string.Empty;
        public string LinkId { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
