using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services.Locations;

/// <summary>Known synthetic fixture metadata only; this is not a parser for real property addresses.</summary>
internal static class SampleLocations
{
    public static PropertyLocation? FromFixtureAddress(string address)
    {
        var parts = address.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return null;
        string[] capeTownAreas = ["Sea Point", "Green Point", "Observatory", "Gardens", "Woodstock", "Century City", "Constantia"];
        if (parts[2] == "Cape Town" && capeTownAreas.Contains(parts[1]))
            return new PropertyLocation { CountryCode = "ZA", Country = "South Africa", Region = "Western Cape", City = "Cape Town", Suburb = parts[1] };
        if (parts[2] == "Pretoria" && parts[1] == "Hatfield")
            return new PropertyLocation { CountryCode = "ZA", Country = "South Africa", Region = "Gauteng", City = "Pretoria", Suburb = "Hatfield" };
        return null;
    }

    public static async Task ConfirmExistingFixturesAsync(PropertyDbContext database, string ownerId, string agencyId)
    {
        var samples = await database.PropertyAssets.Where(item => item.TenantId == agencyId && item.PublicOwnerAccountId == ownerId && item.LocationId == null && item.PropertyDescription.Contains("[PUBLIC-SITE-E2E:")).ToListAsync();
        var updated = 0;
        foreach (var sample in samples)
        {
            if (!sample.CompleteAddress.StartsWith("SAMPLE E2E — ", StringComparison.Ordinal)) continue;
            var location = FromFixtureAddress(sample.CompleteAddress);
            if (location is null) continue;
            sample.Location = await PropertyLocationResolver.ResolveAsync(database, location);
            updated++;
        }
        await database.SaveChangesAsync();
        Console.WriteLine($"Confirmed structured locations for {updated} labelled synthetic properties; real listings were not changed.");
    }
}
