using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data
{
    public static class IdentitySeedData
    {
        public static void EnsureSeeded(IdentityDbContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

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

            var adminUser = context.UserAccounts.FirstOrDefault(u => u.Email == "admin@agency.local");
            if (adminUser == null)
            {
                context.UserAccounts.Add(new UserAccount
                {
                    UserId = "USR-ADMIN",
                    TenantId = "TEN-001",
                    FullName = "Agency Administrator",
                    Email = "admin@agency.local",
                    PasswordHash = "Password123!",
                    SystemRole = "SuperAdmin",
                    AvatarUrl = string.Empty,
                    AccountStatus = "Active",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 🌐 Canonical role accounts: one seeded user per role in the role contract
            // (PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant). Each is an idempotent
            // upsert by email so re-seeding normalizes any drift without duplicating and
            // without ever reassigning a user's primary key value. Passwords are stored
            // as the plain verifier AuthController.Login compares ("Plain-text comparison
            // for local testing build").
            var roleSeedUsers = new[]
            {
                new { Email = "platform.admin@agency.local", UserId = "USR-ADMIN-PLAT", FullName = "Platform Administrator", SystemRole = "PlatformAdmin", TenantId = "TEN-001" },
                new { Email = "agency.admin@agency.local",   UserId = "USR-ADMIN-AGY",  FullName = "Agency Administrator",     SystemRole = "AgencyAdmin",   TenantId = "TEN-001" },
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
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Normalize contract identity. UserId is NOT reassigned here
                    // (it is a stable string primary key and may be referenced by
                    // UserAccessClaims rows); only non-key columns are corrected.
                    existing.TenantId = seedUser.TenantId;
                    existing.SystemRole = seedUser.SystemRole;
                    existing.AccountStatus = "Active";
                    existing.PasswordHash = "Password123!";
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
        }
    }
}
