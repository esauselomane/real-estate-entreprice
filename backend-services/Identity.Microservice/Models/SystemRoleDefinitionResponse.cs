namespace Identity.Microservice.Models
{
    public class SystemRoleDefinitionResponse
    {
        public int RoleDefId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPlatformScoped { get; set; }
        public bool IsAgencyScoped { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<int> PermissionIds { get; set; } = new List<int>();
        public List<PermissionResponse> Permissions { get; set; } = new List<PermissionResponse>();
    }

    public class PermissionResponse
    {
        public int PermissionId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
