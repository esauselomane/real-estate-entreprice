using System.ComponentModel.DataAnnotations;
namespace Identity.Microservice.Models
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty; // e.g. "properties:create"
        [Required]
        [MaxLength(100)]
        public string Module { get; set; } = string.Empty; // e.g. "Properties"
        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // e.g. "create"
        [MaxLength(250)]
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
