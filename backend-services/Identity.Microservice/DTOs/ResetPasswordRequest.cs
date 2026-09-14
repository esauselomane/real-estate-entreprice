using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.DTOs;

public sealed record ResetPasswordRequest
{
    [Required, StringLength(64, MinimumLength = 64)] public string Token { get; init; } = string.Empty;
    [Required, StringLength(256, MinimumLength = 8)] public string NewPassword { get; init; } = string.Empty;
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; init; } = string.Empty;
}
