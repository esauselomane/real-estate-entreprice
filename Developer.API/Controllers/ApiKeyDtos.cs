namespace Developer.API.Controllers
{
    public class GenerateApiKeyRequest
    {
        public string AgencyId { get; set; } = string.Empty;
        public string AgencyName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AllowedIps { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class GenerateApiKeyResponse
    {
        public int Id { get; set; }
        public string AgencyId { get; set; } = string.Empty;
        public string AgencyName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AllowedIps { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ApiKeyInfoResponse
    {
        public int Id { get; set; }
        public string AgencyId { get; set; } = string.Empty;
        public string AgencyName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string KeyPrefix { get; set; } = string.Empty;
        public string? AllowedIps { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsRevoked { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
