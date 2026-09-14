using System.Security.Cryptography;
using System.Text;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Presentation.Email;

namespace Identity.Microservice.Services.Accounts;

/// <summary>Issues hashed, expiring recovery tokens and consumes them atomically with the password change.</summary>
public sealed class PasswordRecoveryService(IdentityDbContext database, IEmailService email, IConfiguration configuration)
{
    public async Task RequestAsync(string address)
    {
        var user = await database.UserAccounts.FirstOrDefaultAsync(account => account.Email == address && account.AccountStatus == "Active");
        if (user == null) return;
        var now = DateTime.UtcNow;
        var recentCutoff = now.AddMinutes(-1);
        if (await database.PasswordRecoveryTokens.AnyAsync(token => token.UserId == user.UserId && token.CreatedAt > recentCutoff)) return;
        var frontend = configuration["Urls:FrontendClient"];
        if (EmailLayout.SafeUrl(frontend) == null) throw new InvalidOperationException("Urls:FrontendClient must be an absolute HTTP(S) URL.");
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        database.PasswordRecoveryTokens.Add(new PasswordRecoveryToken
        {
            TokenHash = HashToken(rawToken), UserId = user.UserId, CreatedAt = now, ExpiresAt = now.AddMinutes(30)
        });
        await database.SaveChangesAsync();
        var link = $"{frontend!.TrimEnd('/')}/?recoveryToken={rawToken}";
        await email.SendEmailAsync(user.Email, "Reset your password",
            $"<p>Hello {EmailLayout.Encode(user.FullName)},</p><p>We received a request to reset your password. This link expires in 30 minutes and can be used once.</p><p><a href=\"{EmailLayout.Encode(link)}\">Reset your password</a></p><p>If you did not request this, you can ignore this email. Your password has not changed.</p>");
    }

    public async Task<bool> ResetAsync(string rawToken, string password)
    {
        if (rawToken.Length != 64 || password.Length < 8 || password.Length > 256) return false;
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;
        await using var transaction = await database.Database.BeginTransactionAsync();
        var token = await database.PasswordRecoveryTokens.AsNoTracking().FirstOrDefaultAsync(item => item.TokenHash == tokenHash && item.ExpiresAt > now);
        if (token == null) return false;
        var user = await database.UserAccounts.FirstOrDefaultAsync(account => account.UserId == token.UserId && account.AccountStatus == "Active");
        if (user == null) return false;
        var consumed = await database.PasswordRecoveryTokens.Where(item => item.TokenHash == tokenHash && item.ExpiresAt > now).ExecuteDeleteAsync();
        if (consumed != 1) return false;
        user.PasswordHash = AccountPasswordHelper.Hash(user, password);
        await database.PasswordRecoveryTokens.Where(item => item.UserId == user.UserId).ExecuteDeleteAsync();
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        await email.SendEmailAsync(user.Email, "Your password has been reset", "<p>Your account password was reset successfully.</p><p>If you did not make this change, contact your agency administrator immediately.</p>");
        return true;
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
