using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

/// <summary>Adds advertised features only to labelled, fictional E2E listings.</summary>
internal static class SamplePublicFeatures
{
    public static async Task SeedAsync(PropertyDbContext database, string ownerId, string agencyId)
    {
        string[] names = ["Parking", "PetsAllowed", "SwimmingPool"];
        var types = await database.AmenityTypes.Where(item => names.Contains(item.Name) && item.IsActive).ToListAsync();
        var samples = await database.PropertyAssets.Include(item => item.SubUnits)
            .Where(item => item.TenantId == agencyId && item.PublicOwnerAccountId == ownerId && item.PropertyDescription.Contains("[PUBLIC-SITE-E2E:")).ToListAsync();
        foreach (var sample in samples)
        {
            foreach (var type in types)
            {
                var exists = await database.PropertyUnitAmenities.AnyAsync(item => item.PropertyId == sample.PropertyId && item.PropertyUnitId == null && item.AmenityTypeId == type.AmenityTypeId);
                if (exists || (type.IsResidentialOnly && sample.PortfolioZoning != "Residential")) continue;
                database.PropertyUnitAmenities.Add(new PropertyUnitAmenity
                {
                    TenantId = agencyId, PropertyId = sample.PropertyId, AmenityTypeId = type.AmenityTypeId,
                    NumberValue = type.RequiresNumber ? 2 : null, BooleanValue = type.RequiresBoolean ? true : null
                });
            }
            if (!sample.IsForRent) continue;
            foreach (var unit in sample.SubUnits)
                unit.AdvertisedDepositAmount ??= (unit.SpecificUnitRentalPrice ?? unit.RentalRate) * 1.5m;
        }
        await database.SaveChangesAsync();
        Console.WriteLine("Added sample amenities and advertised rental deposits to labelled fictional properties only.");
    }
}
