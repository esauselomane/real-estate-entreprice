using System.ComponentModel.DataAnnotations.Schema;
namespace Identity.Microservice.Models
{
    public class RolePermission
    {
        public int RoleDefId { get; set; }
        public int PermissionId { get; set; }
        [ForeignKey("RoleDefId")]
        public SystemRoleDefinition? Role { get; set; }
        [ForeignKey("PermissionId")]
        public Permission? Permission { get; set; }
    }
}
