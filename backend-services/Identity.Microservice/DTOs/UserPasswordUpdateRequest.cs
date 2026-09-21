namespace Identity.Microservice.DTOs;

/// <summary>
/// Admin-initiated password reset for an existing user.
/// Unlike <see cref="ChangePasswordRequest"/> this does NOT require the
/// caller's current password, because a PlatformAdmin / AgencyAdmin is
/// resetting a *different* user's credential, not their own.
/// </summary>
public record class UserPasswordUpdateRequest
{
    public string NewPassword { get; init; } = string.Empty;
}
