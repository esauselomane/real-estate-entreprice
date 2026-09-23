using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Shared.Access.SampleData;

namespace Property.Microservice.Services.SampleData;

/// <summary>Creates the property side of the onboarding scenario once per tenant.</summary>
public sealed class SampleDataSeeder(PropertyDbContext database)
{
    public async Task<SampleDataResult> SeedAsync(SampleDataPlan plan, CancellationToken cancellationToken)
    {
        if (!SampleDataBlueprint.IsValid(plan)) throw new ArgumentException("A valid agency address and coordinates are required.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await SampleDataStorage.LockAsync(database, plan.TenantId, cancellationToken);
        var receipt = await database.SampleDataReceipts.FindAsync([plan.TenantId], cancellationToken);
        if (receipt != null) return JsonSerializer.Deserialize<SampleDataResult>(receipt.ResultJson)!;
        var properties = new List<PropertyAsset>();
        for (var index = 0; index < 5; index++)
        {
            var price = SampleDataBlueprint.Price(index);
            var typeName = SampleDataBlueprint.PropertyTypeName(plan.TenantId, index);
            var propertyType = await FindOrCreatePropertyTypeAsync(typeName, cancellationToken);
            var unitCount = SampleDataBlueprint.UnitCount(plan.TenantId, index);
            var property = new PropertyAsset
            {
                TenantId = plan.TenantId, CompleteAddress = SampleDataBlueprint.PropertyAddress(plan.Address, index),
                PropertyTypeId = propertyType.PropertyTypeId, ErfSpatialSize = (450 + 100 * index).ToString(),
                TitleDeedType = "Freehold", PortfolioZoning = typeName, MarketClass = "Sale", IsForSale = true,
                SalePrice = price, ValuationPrice = price, Latitude = plan.Latitude + (index - 2) * 0.001m,
                Longitude = plan.Longitude + (index - 2) * 0.001m,
                LinkedOwnerId = SampleDataBlueprint.Id(plan.TenantId, "contact", index), CreatedAt = plan.CreatedAt,
                PropertyDescription = $"SAMPLE — Fictional {typeName.ToLowerInvariant()} property with {unitCount} units near {plan.Address}. Approximate demonstration location; this is not a real listing."
            };
            AddUnits(property, typeName, unitCount, price, plan.CreatedAt);
            properties.Add(property);
            database.PropertyAssets.Add(property);
        }
        await database.SaveChangesAsync(cancellationToken);
        var references = new List<SamplePropertyReference>();
        for (var index = 0; index < properties.Count; index++)
            references.Add(new SamplePropertyReference(index, properties[index].PropertyId, properties[index].CompleteAddress, properties[index].SalePrice!.Value, properties[index].PropertyTypeDetails!.Name));
        var result = new SampleDataResult(references);
        database.SampleDataReceipts.Add(new SampleDataReceipt { TenantId = plan.TenantId, ResultJson = JsonSerializer.Serialize(result) });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<PropertyType> FindOrCreatePropertyTypeAsync(string name, CancellationToken cancellationToken)
    {
        var propertyType = await database.PropertyTypes.FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (propertyType != null) return propertyType;
        propertyType = new PropertyType { Name = name };
        database.PropertyTypes.Add(propertyType);
        await database.SaveChangesAsync(cancellationToken);
        return propertyType;
    }

    private static void AddUnits(PropertyAsset property, string typeName, int count, decimal price, DateTime createdAt)
    {
        var label = typeName switch { "Commercial" => "Suite", "Land" => "Plot", _ => "Home" };
        var unitPrice = decimal.Round(price / count, 2);
        for (var index = 0; index < count; index++)
        {
            property.SubUnits.Add(new PropertyUnit
            {
                UnitNumber = $"{label} {index + 1}",
                UnitDescription = $"Sample {typeName.ToLowerInvariant()} {label.ToLowerInvariant()} {index + 1}",
                NetRentableAreaSqM = 50 + index * 10,
                SpecificUnitSalePrice = index == count - 1 ? price - unitPrice * (count - 1) : unitPrice,
                CreatedAt = createdAt
            });
        }
    }

}
