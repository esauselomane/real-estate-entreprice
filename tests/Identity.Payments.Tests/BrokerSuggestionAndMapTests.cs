using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Brochures;
using CRM.Microservice.Services.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class BrokerSuggestionAndMapTests
{
    [Fact]
    public void MapIncludesDistinctMarkersAndOneDottedEnclosure()
    {
        var url = GoogleBrochureMapHelper.BuildUrl([(-33.9, 18.4), (-33.8, 18.5), (-33.9, 18.4), (0, 0)], "sample-key");
        Assert.NotNull(url);
        var decoded = Uri.UnescapeDataString(url!);
        Assert.Contains("markers=color:red|-33.900000,18.400000|-33.800000,18.500000", decoded);
        Assert.Equal(36, decoded.Split("&path=").Length - 1);
        Assert.True(url!.Length < 16384);
    }

    [Theory]
    [InlineData("sample-key", 0, 0)] // Only origin point provided
    [InlineData("", -33.9, 18.4)]   // Missing API key
    public void MapBuildUrlReturnsNullForInvalidInput(string apiKey, double lat, double lon)
    {
        Assert.Null(GoogleBrochureMapHelper.BuildUrl([(lat, lon)], apiKey));
    }

    [Fact]
    public async Task SuggestionsUseFreshMetricsAndIncludeHighestScoringCurrentUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new CRMDbContext(new DbContextOptionsBuilder<CRMDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.BrokerRecords.AddRange(
            new BrokerRecord { BrokerId = "best", TenantId = "agency", AgencyId = "agency", FullName = "Best" },
            new BrokerRecord { BrokerId = "new", TenantId = "agency", AgencyId = "agency", FullName = "New" },
            new BrokerRecord { BrokerId = "inactive", TenantId = "agency", AgencyId = "agency", Status = "Inactive" });
        database.LeadRecords.Add(new LeadRecord { TenantId = "agency", AllocatedBrokerIds = "best", IsConvertedToDeal = true });
        await database.SaveChangesAsync();
        var repository = new BrokerRepository(database, NullLogger<Repository<BrokerRecord>>.Instance);
        var service = new BrokerScoreService(database, repository, new ConfigurationBuilder().Build(), NullLogger<BrokerScoreService>.Instance);
        var suggestions = await service.GetSuggestedBrokersAsync("agency", null, null, null, null, "best");
        Assert.Equal("best", suggestions[0].BrokerId);
        Assert.Equal(100m, suggestions[0].ConversionRate);
        Assert.DoesNotContain(suggestions, broker => broker.BrokerId == "inactive");
        Assert.Equal(0m, suggestions.Single(broker => broker.BrokerId == "new").MatchScore);
    }
}
