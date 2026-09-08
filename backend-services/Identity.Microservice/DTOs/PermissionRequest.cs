using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
    public record class PermissionRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
