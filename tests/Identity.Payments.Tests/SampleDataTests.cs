using System.Security.Claims;
using CRM.Microservice.Data;
using Identity.Microservice.Controllers;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Shared.Access.SampleData;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class SampleDataTests
{
    [Fact]
    public async Task ScenarioIsLinkedAndTenantIsolatedAndReplayDoesNotDuplicate()
    {
        await using var propertyConnection = new SqliteConnection("Data Source=:memory:");
        await using var crmConnection = new SqliteConnection("Data Source=:memory:");
        await propertyConnection.OpenAsync();
        await crmConnection.OpenAsync();
        await using var properties = new PropertyDbContext(new DbContextOptionsBuilder<PropertyDbContext>().UseSqlite(propertyConnection).Options);
        await using var crm = new CRMDbContext(new DbContextOptionsBuilder<CRMDbContext>().UseSqlite(crmConnection).Options);
        await properties.Database.EnsureCreatedAsync();
        await crm.Database.EnsureCreatedAsync();
        var propertySeeder = new Property.Microservice.Services.SampleData.SampleDataSeeder(properties);
        var crmSeeder = new CRM.Microservice.Services.SampleData.SampleDataSeeder(crm);
        foreach (var tenant in new[] { "tenant-a", "tenant-b" })
        {
            var plan = new SampleDataPlan(tenant, "Agency", "admin@example.invalid", "Sandton, Johannesburg", -26.107m, 28.056m, DateTime.UtcNow);
            var result = await propertySeeder.SeedAsync(plan, default);
            await crmSeeder.SeedAsync(new(plan, result), default);
            var repeated = await propertySeeder.SeedAsync(plan, default);
            await crmSeeder.SeedAsync(new(plan, repeated), default);
            Assert.Equal(result.Properties.Select(item => item.PropertyId), repeated.Properties.Select(item => item.PropertyId));
            Assert.Equal(5, await crm.Contacts.CountAsync(item => item.TenantId == tenant));
            Assert.Equal(5, await crm.BrokerRecords.CountAsync(item => item.TenantId == tenant));
            Assert.Equal(5, await crm.LeadRecords.CountAsync(item => item.TenantId == tenant));
            Assert.Equal(1, await crm.CRMCompanies.CountAsync(item => item.TenantId == tenant));
            Assert.Equal(1, await crm.CRMLegalEntities.CountAsync(item => item.TenantId == tenant));
            var deals = await crm.DealRecords.Include(item => item.BrokerSplits).Where(item => item.TenantId == tenant).ToListAsync();
            Assert.Equal(5, deals.Count);
            Assert.Equal(5, await properties.PropertyAssets.CountAsync(item => item.TenantId == tenant));
            foreach (var deal in deals)
            {
                var property = await properties.PropertyAssets.Include(item => item.SubUnits).Include(item => item.PropertyTypeDetails).SingleAsync(item => item.PropertyId == deal.LinkedPropertyId);
                Assert.Equal(tenant, property.TenantId);
                Assert.Contains(property.PropertyTypeDetails!.Name, new[] { "Commercial", "Residential", "Land" });
                Assert.InRange(property.SubUnits.Count, 2, 5);
                Assert.Equal(property.SalePrice, property.SubUnits.Sum(item => item.SpecificUnitSalePrice));
                Assert.Equal(deal.SellerClientId, property.LinkedOwnerId);
                Assert.NotEqual(deal.BuyerClientId, deal.SellerClientId);
                var lead = await crm.LeadRecords.SingleAsync(item => item.LeadId == deal.SourceLeadId);
                Assert.Equal(deal.BuyerClientId, lead.LinkedContactId);
                Assert.Equal(deal.AllocatedBrokerIds, lead.AllocatedBrokerIds);
                Assert.True(lead.IsConvertedToDeal);
                Assert.Equal(property.PropertyTypeDetails.Name, lead.PropertyInterestClass);
                var buyer = await crm.Contacts.FindAsync(deal.BuyerClientId);
                Assert.Equal(property.PropertyTypeDetails.Name, buyer!.PropertyTypeInterests);
                Assert.Equal(tenant, (await crm.BrokerRecords.FindAsync(deal.AllocatedBrokerIds))!.TenantId);
                Assert.Equal(100, Assert.Single(deal.BrokerSplits).SplitPercentage);
                Assert.InRange(Math.Abs(property.Latitude - plan.Latitude), 0, 0.003m);
                Assert.Equal(property.SalePrice, deal.TransactionValue);
            }
        }
    }

    [Fact]
    public async Task OnlyRequestedAgencyCanBeQueuedAndDoubleClickPreservesPlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.TenantAgencies.Add(new TenantAgency { TenantId = "requester", AgencyName = "Requested Agency", ContactEmail = "admin@example.invalid", HeadquartersAddress = "Sandton" });
        database.SampleDataJobs.Add(new SampleDataJob { TenantId = "requester" });
        await database.SaveChangesAsync();
        var controller = new SampleDataAdminController(database) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "platform-admin"), new Claim(ClaimTypes.Role, "PlatformAdmin")], "test")) } } };
        Assert.IsType<NotFoundObjectResult>(await controller.Provision("not-requested", new(-26, 28), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Provision("requester", new(null, null), default));
        Assert.Equal("Requested", (await database.SampleDataJobs.AsNoTracking().SingleAsync()).State);
        Assert.IsType<AcceptedResult>(await controller.Provision("requester", new(-26, 28), default));
        var queued = await database.SampleDataJobs.AsNoTracking().SingleAsync();
        Assert.Equal("Pending", queued.State);
        Assert.Equal("platform-admin", queued.ApprovedBy);
        await controller.Provision("requester", new(-33, 18), default);
        Assert.Equal(queued.PlanJson, (await database.SampleDataJobs.AsNoTracking().SingleAsync()).PlanJson);
        var authorization = Assert.Single(typeof(SampleDataAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("PlatformAdmin,SuperAdmin", authorization.Roles);
    }
}
