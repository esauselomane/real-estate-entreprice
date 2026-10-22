using System.ComponentModel.DataAnnotations;

namespace Developer.API.Models;

public sealed class PublicOwnerAccount
{
    [MaxLength(100)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(100)] public string AgencyId { get; set; } = "";
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(40)] public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class PublicOwnerSession
{
    [MaxLength(100)] public string Id { get; set; } = "";
    [MaxLength(100)] public string OwnerId { get; set; } = "";
    [MaxLength(100)] public string AgencyId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
