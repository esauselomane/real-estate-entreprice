using System.Text.RegularExpressions;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Identity.Microservice.Services.Accounts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class PasswordRecoveryTests
{
    [Fact]
    public async Task RecoveryIsHashedSingleUseAndDoesNotActivateDisabledAccounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.TenantAgencies.Add(new TenantAgency { TenantId = "tenant" });
        var user = new UserAccount { UserId = "user", TenantId = "tenant", Email = "user@example.com", PasswordHash = "old-password" };
        database.UserAccounts.Add(user);
        await database.SaveChangesAsync();
        var email = new CapturingEmail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Urls:FrontendClient"] = "https://app.example.com" }).Build();
        var service = new PasswordRecoveryService(database, email, configuration);
        await service.RequestAsync("unknown@example.com");
        Assert.Empty(email.Body);
        await service.RequestAsync(user.Email);
        var rawToken = Regex.Match(email.Body, "recoveryToken=([A-F0-9]{64})").Groups[1].Value;
        Assert.Equal(64, rawToken.Length);
        Assert.NotEqual(rawToken, (await database.PasswordRecoveryTokens.SingleAsync()).TokenHash);
        Assert.True(await service.ResetAsync(rawToken, "new-password"));
        Assert.NotEqual("new-password", user.PasswordHash);
        Assert.True(AccountPasswordHelper.Verify(user, "new-password"));
        Assert.False(AccountPasswordHelper.Verify(user, "old-password"));
        Assert.False(await service.ResetAsync(rawToken, "another-password"));
        await service.RequestAsync(user.Email);
        rawToken = Regex.Match(email.Body, "recoveryToken=([A-F0-9]{64})").Groups[1].Value;
        var expiringToken = await database.PasswordRecoveryTokens.SingleAsync();
        expiringToken.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await database.SaveChangesAsync();
        Assert.False(await service.ResetAsync(rawToken, "another-password"));
        expiringToken.ExpiresAt = DateTime.UtcNow.AddMinutes(10);
        user.AccountStatus = "De-registered";
        await database.SaveChangesAsync();
        Assert.False(await service.ResetAsync(rawToken, "another-password"));
        Assert.Equal("De-registered", user.AccountStatus);
    }

    [Fact]
    public void LegacyPasswordRemainsUsableWithoutAcceptingHashAsPassword()
    {
        var user = new UserAccount { PasswordHash = "legacy-password" };
        Assert.True(AccountPasswordHelper.Verify(user, "legacy-password"));
        user.PasswordHash = AccountPasswordHelper.Hash(user, "new-password");
        Assert.False(AccountPasswordHelper.Verify(user, user.PasswordHash));
    }

    private sealed class CapturingEmail : IEmailService
    {
        public string Body { get; private set; } = string.Empty;
        public Task SendEmailAsync(string recipient, string subject, string body)
        {
            Body = body;
            return Task.CompletedTask;
        }
        public Task SendPasswordSetupEmailAsync(string recipient, string name, string link, string agency) => Task.CompletedTask;
    }
}
