using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
        public record class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
}
