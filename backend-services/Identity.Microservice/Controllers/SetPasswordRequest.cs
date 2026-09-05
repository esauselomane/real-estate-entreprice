namespace Identity.Microservice.Controllers
{
    public class SetPasswordRequest
    {
        public string ActivationToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
