using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
    public record class SetPasswordRequest
    {
        public string ActivationToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
