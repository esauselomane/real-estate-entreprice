using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.Models;

public sealed class PasswordRecoveryToken
{
    [Key, MaxLength(64)] public string TokenHash { get; set; } = string.Empty;
    [Required] public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
