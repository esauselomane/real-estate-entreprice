namespace Identity.Microservice.Controllers
{
    public class RoleDefinitionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPlatformScoped { get; set; } = false;
        public bool IsAgencyScoped { get; set; } = false;
        public List<int> PermissionIds { get; set; } = new List<int>();
        public bool IsActive { get; set; } = true;
    }
}
