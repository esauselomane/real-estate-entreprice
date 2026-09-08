using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
        public record class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
}
