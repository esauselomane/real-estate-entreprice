namespace Identity.Microservice.DTOs
{
    public record class UserProfileResponse
    {
        public string UserId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string SystemRole { get; init; } = string.Empty;
        public string TenantId { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public string LicenseNumber { get; init; } = string.Empty;
        public DateTime? LicenseExpiry { get; init; }
        public bool OutOfOffice { get; init; }
        public string AccountStatus { get; init; } = string.Empty;
    }

    public record class UpdateProfileRequest
    {
        public string? FullName { get; init; }
        public bool? OutOfOffice { get; init; }
    }
}
