using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
    public record class PermissionCheckRequest
    {
        public string Permission { get; set; } = string.Empty;
    }
}
