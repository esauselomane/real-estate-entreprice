using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
        public record class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }
}
