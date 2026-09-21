using Identity.Microservice.Controllers;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Identity.Microservice.Services.Abstractions;
using Identity.Microservice.Services.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class RegistrationEmailTests
{
    [Fact]
    public async Task TrialOnboardingPersistsTheAccountAndSendsPasswordSetupEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();

        var users = new UserRepository(database, NullLogger<Repository<UserAccount>>.Instance);
        var tenants = new TenantRepository(database, NullLogger<Repository<TenantAgency>>.Instance);
        var accessClaims = new UserAccessClaimRepository(database, NullLogger<Repository<UserAccessClaim>>.Instance);
        var roles = new SystemRoleRepository(database, NullLogger<Repository<SystemRoleDefinition>>.Instance);
        var email = new CapturingEmail();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Urls:FrontendClient"] = "https://app.example.com" })
            .Build();
        var controller = new AuthController(
            database,
            new TokenService(),
            NullLogger<AuthController>.Instance,
            email,
            configuration,
            users,
            tenants,
            roles,
            accessClaims)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.TrialOnboarding(new TrialOnboardingRequest
        {
            AgencyName = "Acme Property Group",
            AdminFullName = "Amina Khan",
            AdminEmail = "admin@example.com",
            BrokerageLicense = "BROKER-123"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("admin@example.com", email.Recipient);
        Assert.Equal("Welcome — set up your account", email.Subject);
        Assert.Contains("/set-password?token=", email.Body);
        Assert.Contains("Acme Property Group", email.Body);
        Assert.Single(database.UserAccounts);
        Assert.Single(database.TenantAgencies);
        Assert.NotNull(ok.Value);
    }

    private sealed class CapturingEmail : IEmailService
    {
        public string Recipient { get; private set; } = string.Empty;
        public string Subject { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        public Task SendEmailAsync(string recipient, string subject, string body)
        {
            Recipient = recipient;
            Subject = subject;
            Body = body;
            return Task.CompletedTask;
        }

        public Task SendPasswordSetupEmailAsync(string recipient, string name, string link, string agency)
        {
            Recipient = recipient;
            Subject = "Welcome — set up your account";
            Body = $"{agency} {link}";
            return Task.CompletedTask;
        }
    }
}
