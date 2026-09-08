using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
        public record class UpdateUserRoleRequest
        {
            public string SystemRole { get; set; } = string.Empty;
        }
}
