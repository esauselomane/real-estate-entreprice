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
        var leads = new LeadCaptureRequestRepository(database, NullLogger<Repository<LeadCaptureRequest>>.Instance);
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
            accessClaims,
            leads)
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
        var savedLead = Assert.Single(database.LeadCaptureRequests);
        Assert.Equal("admin@example.com", savedLead.Email);
        Assert.Equal("onboarding", savedLead.Source);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task TrialOnboardingReturnsBadGatewayAndPersistsLeadWhenEmailDeliveryFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();

        var users = new UserRepository(database, NullLogger<Repository<UserAccount>>.Instance);
        var tenants = new TenantRepository(database, NullLogger<Repository<TenantAgency>>.Instance);
        var accessClaims = new UserAccessClaimRepository(database, NullLogger<Repository<UserAccessClaim>>.Instance);
        var roles = new SystemRoleRepository(database, NullLogger<Repository<SystemRoleDefinition>>.Instance);
        var leads = new LeadCaptureRequestRepository(database, NullLogger<Repository<LeadCaptureRequest>>.Instance);
        var email = new FailingEmail();
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
            accessClaims,
            leads)
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

        var badGateway = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, badGateway.StatusCode);
        var savedLead = Assert.Single(database.LeadCaptureRequests);
        Assert.Equal("admin@example.com", savedLead.Email);
        Assert.Empty(database.UserAccounts);
        Assert.Empty(database.TenantAgencies);
        Assert.Empty(database.UserAccessClaims);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserRegistrationInvitesPendingAccountOrRollsBackOnEmailFailure(bool failDelivery)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.TenantAgencies.Add(new TenantAgency { TenantId = "agency", AgencyName = "Test Agency", OperationalStatus = "Suspended" });
        database.SystemRoles.Add(new SystemRoleDefinition { Name = "Broker", IsActive = true });
        await database.SaveChangesAsync();
        var capturedEmail = new CapturingEmail();
        IEmailService email = failDelivery ? new FailingEmail() : capturedEmail;
        var controller = CreateRegistrationController(database, email);
        var newUser = new UserAccount
        {
            TenantId = "agency", FullName = "Esau", Email = "recipient@example.com", SystemRole = "Broker",
            PasswordHash = "must-not-be-used", IsTrial = true
        };
        var result = await controller.RegisterUser(newUser);
        database.ChangeTracker.Clear();
        if (failDelivery)
        {
            Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
            Assert.Empty(database.UserAccounts);
            Assert.Empty(database.UserAccessClaims);
            return;
        }

        Assert.IsType<OkObjectResult>(result);
        var savedUser = await database.UserAccounts.SingleAsync();
        Assert.Equal("PendingActivation", savedUser.AccountStatus);
        Assert.Empty(savedUser.PasswordHash);
        Assert.False(savedUser.IsTrial);
        Assert.Equal("recipient@example.com", capturedEmail.Recipient);
        Assert.Contains(savedUser.ActivationToken, capturedEmail.Body);
        Assert.Contains("Test Agency", capturedEmail.Body);
        Assert.True(savedUser.ActivationTokenExpiry > DateTime.UtcNow);
        Assert.Single(database.UserAccessClaims);
        var activationResult = await controller.SetPassword(new SetPasswordRequest
        {
            ActivationToken = savedUser.ActivationToken,
            NewPassword = "TestPassword123!", ConfirmPassword = "TestPassword123!"
        });
        Assert.IsType<OkObjectResult>(activationResult);
        Assert.Equal("Active", savedUser.AccountStatus);
        Assert.True(Identity.Microservice.Services.Accounts.AccountPasswordHelper.Verify(savedUser, "TestPassword123!"));
        Assert.Equal("Suspended", (await database.TenantAgencies.SingleAsync()).OperationalStatus);
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("PendingActivation", true)]
    [InlineData("Disabled", false)]
    public async Task AdminPasswordResetPersistsAndLoginUsesNewPassword(string status, bool canLogin)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.TenantAgencies.Add(new TenantAgency { TenantId = "agency", AgencyName = "Test agency", ContactEmail = "agency@example.test" });
        database.UserAccounts.Add(new UserAccount
        {
            UserId = "reset-target", TenantId = "agency", FullName = "Reset Test", Email = "reset@example.test",
            SystemRole = "Broker", AccountStatus = status, PasswordHash = "OldPassword123!",
            ActivationToken = "old-invitation", ActivationTokenExpiry = DateTime.UtcNow.AddDays(1)
        });
        await database.SaveChangesAsync();
        var controller = CreateRegistrationController(database, new CapturingEmail());
        Assert.IsType<OkObjectResult>(await controller.ResetUserPassword("reset-target", new UserPasswordUpdateRequest { NewPassword = "NewPassword123!" }));
        database.ChangeTracker.Clear();
        var user = await database.UserAccounts.SingleAsync();
        Assert.Empty(user.ActivationToken);
        Assert.Null(user.ActivationTokenExpiry);
        Assert.True(Identity.Microservice.Services.Accounts.AccountPasswordHelper.Verify(user, "NewPassword123!"));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginRequest { Email = user.Email, Password = "OldPassword123!" }));
        var login = await controller.Login(new LoginRequest { Email = user.Email, Password = "NewPassword123!" });
        if (canLogin) Assert.IsType<OkObjectResult>(login);
        else Assert.IsType<UnauthorizedObjectResult>(login);
    }

    private static AuthController CreateRegistrationController(IdentityDbContext database, IEmailService email)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Urls:FrontendClient"] = "https://app.example.com" }).Build();
        var controller = new AuthController(database, new TokenService(), NullLogger<AuthController>.Instance, email, configuration,
            new UserRepository(database, NullLogger<Repository<UserAccount>>.Instance),
            new TenantRepository(database, NullLogger<Repository<TenantAgency>>.Instance),
            new SystemRoleRepository(database, NullLogger<Repository<SystemRoleDefinition>>.Instance),
            new UserAccessClaimRepository(database, NullLogger<Repository<UserAccessClaim>>.Instance),
            new LeadCaptureRequestRepository(database, NullLogger<Repository<LeadCaptureRequest>>.Instance));
        var identity = new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "PlatformAdmin")], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
        return controller;
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

    private sealed class FailingEmail : IEmailService
    {
        public Task SendEmailAsync(string recipient, string subject, string body) =>
            throw new InvalidOperationException("Email provider unavailable");

        public Task SendPasswordSetupEmailAsync(string recipient, string name, string link, string agency) =>
            throw new InvalidOperationException("Email provider unavailable");
    }
}
