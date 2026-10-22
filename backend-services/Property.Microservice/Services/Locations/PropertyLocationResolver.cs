using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Property.Microservice.Services.Locations;

/// <summary>Reuses confirmed address hierarchies; equal suburb names in different regions remain distinct.</summary>
public static class PropertyLocationResolver
{
    public static string CreateIdentity(PropertyLocation location)
    {
        string[] parts = [location.CountryCode, location.Region, location.District, location.City, location.Suburb];
        var normalizedParts = parts.Select(Normalize).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalizedParts))));
    }

    private static string Normalize(string value) => string.Join(" ", (value ?? "").Trim().Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static string Label(PropertyLocation location)
    {
        string[] parts = [location.Suburb, location.City, location.District, location.Region, string.IsNullOrWhiteSpace(location.Country) ? location.CountryCode : location.Country];
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static async Task<PropertyLocation?> ResolveAsync(PropertyDbContext database, PropertyLocation? supplied, CancellationToken cancellationToken = default)
    {
        if (supplied is null) return null;
        var location = new PropertyLocation
        {
            CountryCode = supplied.CountryCode.Trim().ToUpperInvariant(), Country = (supplied.Country ?? "").Trim(),
            Region = (supplied.Region ?? "").Trim(), District = (supplied.District ?? "").Trim(), City = (supplied.City ?? "").Trim(), Suburb = (supplied.Suburb ?? "").Trim()
        };
        location.LocationId = CreateIdentity(location);
        var tracked = database.PropertyLocations.Local.FirstOrDefault(item => item.LocationId == location.LocationId);
        if (tracked is not null) return tracked;
        if (database.Database.IsSqlServer())
        {
            // A range lock prevents concurrent captures from creating duplicate catalogue entries.
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO PropertyLocations (LocationId, CountryCode, Country, Region, District, City, Suburb)
                SELECT {location.LocationId}, {location.CountryCode}, {location.Country}, {location.Region}, {location.District}, {location.City}, {location.Suburb}
                WHERE NOT EXISTS (SELECT 1 FROM PropertyLocations WITH (UPDLOCK, HOLDLOCK) WHERE LocationId = {location.LocationId});
                """, cancellationToken);
            return await database.PropertyLocations.SingleAsync(item => item.LocationId == location.LocationId, cancellationToken);
        }
        var existing = await database.PropertyLocations.FindAsync([location.LocationId], cancellationToken);
        if (existing is not null) return existing;
        database.PropertyLocations.Add(location);
        return location;
    }
}
