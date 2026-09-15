using Identity.Microservice.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.Microservice.Services.Accounts;

/// <summary>Hashes new passwords and supports existing plaintext accounts during migration.</summary>
public static class AccountPasswordHelper
{
    private const string Prefix = "identity-v3:";
    public static string Hash(UserAccount user, string password) => Prefix + new PasswordHasher<UserAccount>().HashPassword(user, password);

    public static bool Verify(UserAccount user, string password)
    {
        if (!user.PasswordHash.StartsWith(Prefix, StringComparison.Ordinal))
            return user.PasswordHash == password;
        try
        {
            return new PasswordHasher<UserAccount>().VerifyHashedPassword(user, user.PasswordHash[Prefix.Length..], password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException) { return false; }
    }
}
