using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data
{
    public static class IdentitySeedData
    {
        public static void EnsureSeeded(IdentityDbContext context, IConfiguration configuration)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var supportEmail = configuration["Email:SupportAddress"] ?? "hello@realestate.example";
            var adminEmail = configuration["Email:AdminEmail"] ?? "admin@agency.local";
            var platformAdminEmail = configuration["Email:PlatformAdminEmail"] ?? "platform.admin@agency.local";
            var agencyAdminEmail = configuration["Email:AgencyAdminEmail"] ?? "agency.admin@agency.local";

            context.Database.EnsureCreated();

            // EnsureCreated is a no-op against a pre-existing database, so provision our
            // configurable role-definitions table via raw DDL when it is missing.
            var rolesTableExists = context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SystemRoles'")
                .AsEnumerable().FirstOrDefault();
            if (rolesTableExists == 0)
            {
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE [SystemRoles] (
                        [RoleDefId] int NOT NULL IDENTITY(1,1),
                        [Name] nvarchar(450) NOT NULL,
                        [Description] nvarchar(max) NOT NULL DEFAULT '',
                        [IsPlatformScoped] bit NOT NULL DEFAULT 0,
                        [IsAgencyScoped] bit NOT NULL DEFAULT 0,
                        [PermissionsJson] nvarchar(max) NOT NULL DEFAULT '[]',
                        [IsActive] bit NOT NULL DEFAULT 1,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT [PK_SystemRoles] PRIMARY KEY ([RoleDefId]),
                        CONSTRAINT [AK_SystemRoles_Name] UNIQUE ([Name])
                    );
                 ");
             }

             // 📋 Public lead/intake capture table — fed by the marketing website
             // (Bibsy chatbot + pricing page). Provisioned via raw DDL because
             // EnsureCreated is a no-op against a pre-existing database, mirroring the
             // SystemRoles bootstrapping above.
             var leadTableExists = context.Database.SqlQueryRaw<int>(
                 "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadCaptureRequests'")
                 .AsEnumerable().FirstOrDefault();
             if (leadTableExists == 0)
             {
                 context.Database.ExecuteSqlRaw(@"
                     CREATE TABLE [LeadCaptureRequests] (
                         [Id] int NOT NULL IDENTITY(1,1),
                         [FullName] nvarchar(max) NOT NULL,
                         [Email] nvarchar(max) NOT NULL,
                         [Phone] nvarchar(max) NULL,
                         [Company] nvarchar(max) NULL,
                         [UserType] int NOT NULL DEFAULT 1,
                         [Package] int NULL,
                         [DemoRequested] bit NOT NULL DEFAULT 0,
                         [Message] nvarchar(max) NULL,
                         [Source] nvarchar(max) NULL,
                         [Status] int NOT NULL DEFAULT 0,
                         [HandledBy] nvarchar(max) NULL,
                         [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                         [UpdatedAt] datetime2 NULL,
                         CONSTRAINT [PK_LeadCaptureRequests] PRIMARY KEY ([Id])
                     );
                 ");
             }

              // 📋 Internal workflow actions — SuperAdmin-only tracking of callbacks,
              // quotes, payments and agency provisioning for leads.
              var workflowTableExists = context.Database.SqlQueryRaw<int>(
                  "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadWorkflows'")
                  .AsEnumerable().FirstOrDefault();
              if (workflowTableExists == 0)
              {
                  context.Database.ExecuteSqlRaw(@"
                      CREATE TABLE [LeadWorkflows] (
                          [Id] int NOT NULL IDENTITY(1,1),
                          [LeadId] int NOT NULL,
                          [ActionType] int NOT NULL,
                          [Notes] nvarchar(max) NULL,
                          [PerformedBy] nvarchar(max) NULL,
                          [QuoteAmount] decimal(18,2) NULL,
                          [Currency] nvarchar(max) NULL DEFAULT 'ZAR',
                          [PaymentReference] nvarchar(max) NULL,
                          [AgencyId] nvarchar(max) NULL,
                          [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                          CONSTRAINT [PK_LeadWorkflows] PRIMARY KEY ([Id]),
                          CONSTRAINT [FK_LeadWorkflows_LeadCaptureRequests_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [LeadCaptureRequests] ([Id]) ON DELETE CASCADE
                      );
                  ");
              }

              // 💰 Pricing packages — configurable plans for the marketing website.
              var pricingTableExists = context.Database.SqlQueryRaw<int>(
                  "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PricingPackages'")
                  .AsEnumerable().FirstOrDefault();
              if (pricingTableExists == 0)
              {
                  context.Database.ExecuteSqlRaw(@"
                      CREATE TABLE [PricingPackages] (
                          [Id] int NOT NULL IDENTITY(1,1),
                          [Code] nvarchar(100) NOT NULL,
                          [Name] nvarchar(200) NOT NULL,
                          [Description] nvarchar(max) NULL,
                          [MonthlyPrice] decimal(18,2) NULL,
                          [AnnualPrice] decimal(18,2) NULL,
                          [FeaturesJson] nvarchar(max) NOT NULL DEFAULT '[]',
                          [SortOrder] int NOT NULL DEFAULT 0,
                          [IsActive] bit NOT NULL DEFAULT 1,
                          [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                          [UpdatedAt] datetime2 NULL,
                          CONSTRAINT [PK_PricingPackages] PRIMARY KEY ([Id]),
                          CONSTRAINT [AK_PricingPackages_Code] UNIQUE ([Code])
                      );
                  ");
              }

              var existingTenants = context.TenantAgencies.Count();
            if (existingTenants < 1000)
            {
                var tenantItems = new List<TenantAgency>();
                for (var i = existingTenants + 1; i <= 1000; i++)
                {
                    tenantItems.Add(new TenantAgency
                    {
                        TenantId = $"TEN-{i:000}",
                        AgencyName = $"Agency {i}",
                        LogoInitials = $"A{i:000}".Substring(0, 2).ToUpper(),
                        ContactEmail = $"ops{i}@agency{i}.local",
                        HeadquartersAddress = $"{i} Commercial Avenue, Cape Town, South Africa",
                        OperationalStatus = i % 5 == 0 ? "Pending" : "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                 context.TenantAgencies.AddRange(tenantItems);
             }

             var trialTenant = context.TenantAgencies.FirstOrDefault(t => t.TenantId == "TEN-001");
             if (trialTenant == null)
             {
                 trialTenant = new TenantAgency
                 {
                     TenantId = "TEN-001",
                     AgencyName = "Agency 1",
                     LogoInitials = "A1",
                     ContactEmail = "ops1@agency1.local",
                     HeadquartersAddress = "1 Commercial Avenue, Cape Town, South Africa",
                     OperationalStatus = "TrialPending",
                     CreatedAt = DateTime.UtcNow.AddDays(-1)
                 };
                 context.TenantAgencies.Add(trialTenant);
             }
             trialTenant.IsTrial = true;
             trialTenant.TrialStartDate = DateTime.UtcNow;
             trialTenant.TrialEndDate = DateTime.UtcNow.AddDays(14);
             trialTenant.OperationalStatus = "TrialPending";

              var adminUser = context.UserAccounts.FirstOrDefault(u => u.Email == adminEmail);
             if (adminUser == null)
             {
                 context.UserAccounts.Add(new UserAccount
                 {
                     UserId = "USR-ADMIN",
                     TenantId = "TEN-001",
                     FullName = "Agency Administrator",
                     Email = adminEmail,
                     PasswordHash = "Password123!",
                     SystemRole = "SuperAdmin",
                     AvatarUrl = string.Empty,
                     AccountStatus = "Active",
                     IsTrial = true,
                     TrialEndDate = DateTime.UtcNow.AddDays(14),
                     CreatedAt = DateTime.UtcNow
                 });
             }
             else
             {
                 adminUser.IsTrial = true;
                 adminUser.TrialEndDate = DateTime.UtcNow.AddDays(14);
             }

            // 🌐 Canonical role accounts: one seeded user per role in the role contract
            // (PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant). Each is an idempotent
            // upsert by email so re-seeding normalizes any drift without duplicating and
            // without ever reassigning a user's primary key value. Passwords are stored
            // as the plain verifier AuthController.Login compares ("Plain-text comparison
            // for local testing build").
            var roleSeedUsers = new[]
            {
                new { Email = platformAdminEmail, UserId = "USR-ADMIN-PLAT", FullName = "Platform Administrator", SystemRole = "PlatformAdmin", TenantId = "TEN-001" },
                new { Email = agencyAdminEmail,   UserId = "USR-ADMIN-AGY",  FullName = "Agency Administrator",     SystemRole = "AgencyAdmin",   TenantId = "TEN-001" },
                new { Email = "broker@agency.local",          UserId = "USR-BROKER-01",  FullName = "Sales Broker",             SystemRole = "Broker",         TenantId = "TEN-001" },
                new { Email = "owner@agency.local",           UserId = "USR-OWNER-01",   FullName = "Property Owner",          SystemRole = "Owner",          TenantId = "TEN-001" },
                new { Email = "tenant@agency.local",          UserId = "USR-TENANT-01",  FullName = "Rental Tenant",           SystemRole = "Tenant",         TenantId = "TEN-001" }
            };
             foreach (var seedUser in roleSeedUsers)
             {
                 var existing = context.UserAccounts.FirstOrDefault(u => u.Email == seedUser.Email);
                 if (existing == null)
                 {
                     context.UserAccounts.Add(new UserAccount
                     {
                         UserId = seedUser.UserId,
                         TenantId = seedUser.TenantId,
                         FullName = seedUser.FullName,
                         Email = seedUser.Email,
                         PasswordHash = "Password123!",
                         SystemRole = seedUser.SystemRole,
                         AvatarUrl = string.Empty,
                         AccountStatus = "Active",
                         IsTrial = seedUser.TenantId == "TEN-001",
                         TrialEndDate = seedUser.TenantId == "TEN-001" ? DateTime.UtcNow.AddDays(14) : null,
                         CreatedAt = DateTime.UtcNow
                     });
                 }
                 else
                 {
                     existing.TenantId = seedUser.TenantId;
                     existing.SystemRole = seedUser.SystemRole;
                     existing.AccountStatus = "Active";
                     existing.PasswordHash = "Password123!";
                     existing.IsTrial = seedUser.TenantId == "TEN-001";
                     existing.TrialEndDate = seedUser.TenantId == "TEN-001" ? DateTime.UtcNow.AddDays(14) : null;
                 }
             }

            if (context.UserAccounts.Count() < 1000)
            {
                var usersToAdd = new List<UserAccount>();
                var userIndex = context.UserAccounts.Count() + 1;
                for (var i = userIndex; i <= 1000; i++)
                {
                    var tenantId = $"TEN-{((i - 1) % 100) + 1:000}";
                    usersToAdd.Add(new UserAccount
                    {
                        UserId = $"USR-{i:0000}",
                        TenantId = tenantId,
                        FullName = $"User {i}",
                        Email = $"user{i}@agency.local",
                        PasswordHash = "Password123!",
                        SystemRole = i % 5 == 0 ? "Manager" : i % 3 == 0 ? "Agent" : "Admin",
                        AvatarUrl = string.Empty,
                        AccountStatus = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.UserAccounts.AddRange(usersToAdd);
            }

            context.SaveChanges();

            var existingClaimUserIds = context.UserAccessClaims.Select(c => c.UserId).ToHashSet();
            var claimsToAdd = new List<UserAccessClaim>();

            foreach (var user in context.UserAccounts.ToList())
            {
                if (!existingClaimUserIds.Contains(user.UserId))
                {
                    claimsToAdd.Add(new UserAccessClaim
                    {
                        UserId = user.UserId,
                        CanDeleteProperties = user.SystemRole == "Manager" || user.SystemRole == "SuperAdmin",
                        CanExportFinancialReports = true,
                        CanBypassValuationStages = user.SystemRole == "SuperAdmin"
                    });
                }
            }

             if (claimsToAdd.Any())
             {
                 context.UserAccessClaims.AddRange(claimsToAdd);
                 context.SaveChanges();
             }

            // 🔐 Seed configurable system role definitions — the source of truth for the
            // System Admin "Roles" configuration tab and the provisioning authority matrix.
            var roleDefs = new[]
            {
                new SystemRoleDefinition { Name = "PlatformAdmin", Description = "Platform-wide administrator with full provisioning authority.", IsPlatformScoped = true, IsAgencyScoped = false, PermissionsJson = "[\"CanDeleteProperties\",\"CanExportFinancialReports\",\"CanBypassValuationStages\"]" },
                new SystemRoleDefinition { Name = "AgencyAdmin",   Description = "Agency-level administrator managing brokers and owners.", IsPlatformScoped = false, IsAgencyScoped = true, PermissionsJson = "[\"CanExportFinancialReports\",\"CanBypassValuationStages\"]" },
                new SystemRoleDefinition { Name = "Broker",        Description = "Sales broker working a pipeline of deals.", IsPlatformScoped = false, IsAgencyScoped = true, PermissionsJson = "[]" },
                new SystemRoleDefinition { Name = "Owner",         Description = "Property owner surfacing leads and interest.", IsPlatformScoped = false, IsAgencyScoped = false, PermissionsJson = "[]" },
                new SystemRoleDefinition { Name = "Tenant",        Description = "Rental tenant with maintenance request access.", IsPlatformScoped = false, IsAgencyScoped = false, PermissionsJson = "[]" }
            };
            foreach (var rd in roleDefs)
            {
                var existing = context.SystemRoles.FirstOrDefault(r => r.Name == rd.Name);
                if (existing == null)
                {
                    context.SystemRoles.Add(new SystemRoleDefinition
                    {
                        Name = rd.Name,
                        Description = rd.Description,
                        IsPlatformScoped = rd.IsPlatformScoped,
                        IsAgencyScoped = rd.IsAgencyScoped,
                        PermissionsJson = rd.PermissionsJson,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Description = rd.Description;
                    existing.IsPlatformScoped = rd.IsPlatformScoped;
                    existing.IsAgencyScoped = rd.IsAgencyScoped;
                    existing.PermissionsJson = rd.PermissionsJson;
                    existing.IsActive = true;
                }
            }
            context.SaveChanges();

            // 💰 Seed configurable pricing packages for the marketing website.
            var pricingPackages = new[]
            {
                new PricingPackage
                {
                    Code = "SME_AGENCY",
                    Name = "SME Agency",
                    Description = "Ideal for small agencies getting started with digital deal management.",
                    MonthlyPrice = 299.00m,
                    AnnualPrice = 2990.00m,
                    FeaturesJson = "[\"Up to 5 users\",\"20 property listings\",\"Basic brochure generation\",\"Email support\",\"Standard CRM pipeline\"]",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PricingPackage
                {
                    Code = "PRIVATE",
                    Name = "Private",
                    Description = "For growing agencies that need more power and reach.",
                    MonthlyPrice = 799.00m,
                    AnnualPrice = 7990.00m,
                    FeaturesJson = "[\"Up to 20 users\",\"Unlimited listings\",\"Advanced brochure + image processing\",\"Priority support\",\"Market syndication\",\"Custom branding\"]",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PricingPackage
                {
                    Code = "ENTERPRISE",
                    Name = "Enterprise",
                    Description = "Full-scale platform access for large organizations with custom needs.",
                    MonthlyPrice = 1999.00m,
                    AnnualPrice = 19990.00m,
                    FeaturesJson = "[\"Unlimited users\",\"Unlimited listings\",\"Dedicated account manager\",\"API access\",\"Custom integrations\",\"On-premise option\",\"SLA guarantee\",\"Advanced analytics\"]",
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
            foreach (var pkg in pricingPackages)
            {
                var existing = context.PricingPackages.FirstOrDefault(p => p.Code == pkg.Code);
                if (existing == null)
                {
                    context.PricingPackages.Add(pkg);
                }
                else
                {
                    existing.Name = pkg.Name;
                    existing.Description = pkg.Description;
                    existing.MonthlyPrice = pkg.MonthlyPrice;
                    existing.AnnualPrice = pkg.AnnualPrice;
                    existing.FeaturesJson = pkg.FeaturesJson;
                    existing.SortOrder = pkg.SortOrder;
                    existing.IsActive = pkg.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            context.SaveChanges();
        }
    }
}
