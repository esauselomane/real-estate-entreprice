using Shared.Presentation.Email;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation;
using Identity.Microservice.Services.Implementation.Payments;
using Identity.Microservice.Services;
using Identity.Microservice.Authorization;
using Identity.Microservice.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransactionalEmail(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHttpClient();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Suppress pending model changes warning — we use EnsureCreated + raw SQL
    // for schema evolution, not EF migrations. Properties added to the model
    // are handled via raw SQL ALTER TABLE statements at startup.
    options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISystemRoleRepository, SystemRoleRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
builder.Services.AddScoped<IUserAccessClaimRepository, UserAccessClaimRepository>();
builder.Services.AddScoped<IRepository<LeadCaptureRequest>, LeadCaptureRequestRepository>();
builder.Services.AddScoped<ILeadWorkflowRepository, LeadWorkflowRepository>();
builder.Services.AddScoped<IPricingPackageRepository, PricingPackageRepository>();
builder.Services.AddScoped<IPeachCustomerRepository, PeachCustomerRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();
builder.Services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

// Payment Gateway HTTP Clients
builder.Services.AddHttpClient<PeachPaymentsGateway>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<PayFastGateway>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Payment Services
builder.Services.AddScoped<IPaymentGateway>(ResolvePeachGateway);
builder.Services.AddScoped<IPaymentGateway>(ResolvePayFastGateway);
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("Payments"));
builder.Services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
builder.Services.AddScoped<PaymentOrchestrationService>();
builder.Services.AddHostedService<BillingNotificationWorker>();
builder.Services.AddScoped<IBillingNotificationEmailSender, BillingNotificationEmailSender>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<Identity.Microservice.Services.Accounts.PasswordRecoveryService>();

//Made a small change to force rebuild
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission", policy =>
        policy.Requirements.Add(new PermissionRequirement(string.Empty)));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.Configure<PeachPaymentsSettings>(builder.Configuration.GetSection(PeachPaymentsSettings.SectionName));
builder.Services.Configure<PayFastSettings>(builder.Configuration.GetSection(PayFastSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddHttpContextAccessor();
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Identity.Microservice";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RealEstateApp.Client";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    db.Database.EnsureCreated();
    // 🎯 Fix: If the __EFMigrationsHistory table exists from EnsureCreated() but lacks
    // the ProductVersion column (pre-EF-Core-10 schema), or if it doesn't exist at all,
    // seed the migration history so Migrate() doesn't try to re-create tables.
    db.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
        BEGIN
            CREATE TABLE [__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            );
        END
        ELSE IF COL_LENGTH(N'[__EFMigrationsHistory]', N'ProductVersion') IS NULL
        BEGIN
            ALTER TABLE [__EFMigrationsHistory] ADD [ProductVersion] nvarchar(32) NOT NULL CONSTRAINT DF_EFH_History_ProductVersion DEFAULT N'';
        END
        IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260909224756_InitialCreate')
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20260909224756_InitialCreate', N'10.0.0');
    ");
    // 🎯 Apply any future migrations added via `dotnet ef migrations add`
    db.Database.Migrate();
    // 🪪 Add LicenseNumber + LicenseExpiry columns for broker licensing
    // and BrokerageLicense column for agency-level brokerage license
    db.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH(N'[UserAccounts]', N'LicenseNumber') IS NULL
            ALTER TABLE [UserAccounts] ADD [LicenseNumber] nvarchar(100) NOT NULL CONSTRAINT DF_User_LicenseNumber DEFAULT N'';
        IF COL_LENGTH(N'[UserAccounts]', N'LicenseExpiry') IS NULL
            ALTER TABLE [UserAccounts] ADD [LicenseExpiry] datetime2 NULL;
        -- 🏢 Agency-level brokerage license column
        IF COL_LENGTH(N'[TenantAgencies]', N'BrokerageLicense') IS NULL
            ALTER TABLE [TenantAgencies] ADD [BrokerageLicense] nvarchar(256) NOT NULL CONSTRAINT DF_TenantAgencies_BrokerageLicense DEFAULT N'';
        -- 🚫 Out-of-office flag for brokers (self-toggled via profile pop-out)
        IF COL_LENGTH(N'[UserAccounts]', N'OutOfOffice') IS NULL
            ALTER TABLE [UserAccounts] ADD [OutOfOffice] bit NOT NULL CONSTRAINT DF_User_OutOfOffice DEFAULT 0;
    ");
    var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var userAccessClaimRepository = scope.ServiceProvider.GetRequiredService<IUserAccessClaimRepository>();
    var permissionRepository = scope.ServiceProvider.GetRequiredService<IPermissionRepository>();
    var systemRoleRepository = scope.ServiceProvider.GetRequiredService<ISystemRoleRepository>();
    var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
    var pricingPackageRepository = scope.ServiceProvider.GetRequiredService<IPricingPackageRepository>();

    await IdentitySeedData.EnsureSeededAsync(db, config, tenantRepository, userRepository, userAccessClaimRepository, permissionRepository, systemRoleRepository, rolePermissionRepository, pricingPackageRepository);
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "identity" }));
app.Run();

static IPaymentGateway ResolvePeachGateway(IServiceProvider services) => services.GetRequiredService<PeachPaymentsGateway>();
static IPaymentGateway ResolvePayFastGateway(IServiceProvider services) => services.GetRequiredService<PayFastGateway>();
