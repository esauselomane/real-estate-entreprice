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

            foreach (var user in context.UserAccounts.ToList())
            {
                if (!context.UserAccessClaims.Any(c => c.UserId == user.UserId))
                {
                    context.UserAccessClaims.Add(new UserAccessClaim
                    {
                        UserId = user.UserId,
                        CanDeleteProperties = user.SystemRole == "Manager" || user.SystemRole == "SuperAdmin",
                        CanExportFinancialReports = true,
                        CanBypassValuationStages = user.SystemRole == "SuperAdmin"
                    });
                }
            }

            context.SaveChanges();
        }
    }
}
