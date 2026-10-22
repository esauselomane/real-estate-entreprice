using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;

namespace Property.Microservice.Services.PublicListings;

/// <summary>Upgrades existing databases without publishing any existing property.</summary>
public static class PublicListingSchema
{
    public static void EnsurePropertyTypes(PropertyDbContext database)
    {
        using var transaction = database.Database.BeginTransaction();
        if (database.Database.IsSqlServer())
            database.Database.ExecuteSqlRaw("DECLARE @result int; EXEC @result = sp_getapplock @Resource='property-type-catalogue', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 50001, 'Property type catalogue is busy.', 1;");
        string[] supportedTypes = ["Residential", "Commercial", "Industrial", "Retail", "Land", "Room share/let"];
        var existingNames = database.PropertyTypes.Select(type => type.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in supportedTypes)
        {
            if (!existingNames.Contains(name)) database.PropertyTypes.Add(new Property.Microservice.Models.PropertyType { Name = name });
        }
        database.SaveChanges();
        var roomTypeId = database.PropertyTypes.Single(type => type.Name == "Room share/let").PropertyTypeId;
        // Classify existing room offerings without altering their sale/letting purposes or publication status.
        database.PropertyAssets.Where(item => item.RoomLettingType != "None" && item.PropertyTypeId != roomTypeId)
            .ExecuteUpdate(setters => setters.SetProperty(item => item.PropertyTypeId, roomTypeId));
        transaction.Commit();
    }

    public static void EnsureCreated(PropertyDbContext database)
    {
        database.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('PropertyAssets', 'PublicPublishedAt') IS NULL
                ALTER TABLE PropertyAssets ADD PublicPublishedAt datetime2 NULL;
            IF COL_LENGTH('PropertyAssets', 'PublicLettingExpiresAt') IS NULL
                ALTER TABLE PropertyAssets ADD PublicLettingExpiresAt datetime2 NULL;
            IF COL_LENGTH('PropertyUnits', 'AdvertisedDepositAmount') IS NULL
                ALTER TABLE PropertyUnits ADD AdvertisedDepositAmount decimal(18,2) NULL;
            IF OBJECT_ID('PropertyLocations', 'U') IS NULL
                CREATE TABLE PropertyLocations (
                    LocationId nvarchar(64) NOT NULL PRIMARY KEY, CountryCode nvarchar(2) NOT NULL,
                    Country nvarchar(150) NOT NULL, Region nvarchar(150) NOT NULL, District nvarchar(150) NOT NULL,
                    City nvarchar(150) NOT NULL, Suburb nvarchar(150) NOT NULL);
            IF COL_LENGTH('PropertyAssets', 'LocationId') IS NULL
                ALTER TABLE PropertyAssets ADD LocationId nvarchar(64) NULL;
            IF COL_LENGTH('PropertyAssets', 'GooglePlaceId') IS NULL
                ALTER TABLE PropertyAssets ADD GooglePlaceId nvarchar(300) NULL;
            IF COL_LENGTH('PropertyAssets', 'PostalCode') IS NULL
                ALTER TABLE PropertyAssets ADD PostalCode nvarchar(40) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PropertyAssets_LocationId' AND object_id = OBJECT_ID('PropertyAssets'))
                EXEC('CREATE INDEX IX_PropertyAssets_LocationId ON PropertyAssets(LocationId)');
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PropertyAssets_PropertyLocations_LocationId')
                EXEC('ALTER TABLE PropertyAssets ADD CONSTRAINT FK_PropertyAssets_PropertyLocations_LocationId FOREIGN KEY (LocationId) REFERENCES PropertyLocations(LocationId)');
            IF COL_LENGTH('PropertyAssets', 'RoomLettingType') IS NULL
                ALTER TABLE PropertyAssets ADD RoomLettingType nvarchar(20) NOT NULL DEFAULT 'None';
            IF COL_LENGTH('PropertyAssets', 'IsPublicListing') IS NULL
                ALTER TABLE PropertyAssets ADD IsPublicListing bit NOT NULL DEFAULT 0;
            IF COL_LENGTH('PropertyAssets', 'IsHotProperty') IS NULL
                ALTER TABLE PropertyAssets ADD IsHotProperty bit NOT NULL DEFAULT 0;
            IF COL_LENGTH('PropertyAssets', 'PublicReviewStatus') IS NULL
                ALTER TABLE PropertyAssets ADD PublicReviewStatus nvarchar(30) NOT NULL DEFAULT 'Draft';
            IF COL_LENGTH('PropertyAssets', 'PublicOwnerAccountId') IS NULL
                ALTER TABLE PropertyAssets ADD PublicOwnerAccountId nvarchar(100) NULL;
            IF COL_LENGTH('PropertyAssets', 'PublicOwnerName') IS NULL
                ALTER TABLE PropertyAssets ADD PublicOwnerName nvarchar(200) NOT NULL DEFAULT '';
            IF COL_LENGTH('PropertyAssets', 'PublicOwnerEmail') IS NULL
                ALTER TABLE PropertyAssets ADD PublicOwnerEmail nvarchar(254) NOT NULL DEFAULT '';
            IF COL_LENGTH('PropertyAssets', 'PublicOwnerPhone') IS NULL
                ALTER TABLE PropertyAssets ADD PublicOwnerPhone nvarchar(40) NOT NULL DEFAULT '';
            IF COL_LENGTH('PropertyAssets', 'PublicSubmittedAt') IS NULL
                ALTER TABLE PropertyAssets ADD PublicSubmittedAt datetime2 NULL;
            """);
        database.Database.ExecuteSqlRaw("""
            UPDATE PropertyAssets SET PublicPublishedAt = COALESCE(PublicPublishedAt, SYSUTCDATETIME()),
                PublicLettingExpiresAt = DATEADD(month, 2, SYSUTCDATETIME())
            WHERE IsPublicListing = 1 AND PublicReviewStatus = 'Approved' AND IsForRent = 1 AND PublicLettingExpiresAt IS NULL;
            """);
    }
}
