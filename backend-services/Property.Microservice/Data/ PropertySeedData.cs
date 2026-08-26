using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Property.Microservice.Data
{
    public static class PropertySeedData
    {
        public static void EnsureSeeded(PropertyDbContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Database.EnsureCreated();

            if (!context.PropertyTypes.Any())
            {
                context.PropertyTypes.AddRange(
                    new Property.Microservice.Models.PropertyType { Name = "Industrial" },
                    new Property.Microservice.Models.PropertyType { Name = "Retail" },
                    new Property.Microservice.Models.PropertyType { Name = "Commercial" },
                    new Property.Microservice.Models.PropertyType { Name = "Residential" }
                );
            }

            context.SaveChanges();

            if (!context.ExternalPlatforms.Any())
            {
                context.ExternalPlatforms.AddRange(
                    new Property.Microservice.Models.ExternalPlatform { PlatformName = "Property24", BaseUrlAddress = "https://property24.com" },
                    new Property.Microservice.Models.ExternalPlatform { PlatformName = "PrivateProperty", BaseUrlAddress = "https://privateproperty.co.za" },
                    new Property.Microservice.Models.ExternalPlatform { PlatformName = "Gumtree", BaseUrlAddress = "https://gumtree.co.za" }
                );
            }

            context.SaveChanges();

            if (context.PropertyAssets.Count() < 1000)
            {
                var propertyTypes = context.PropertyTypes.ToList();
                var assets = new List<Property.Microservice.Models.PropertyAsset>();
                for (var i = context.PropertyAssets.Count() + 1; i <= 1000; i++)
                {
                    var propertyType = propertyTypes[(i - 1) % propertyTypes.Count];
                    var isRental = i % 2 == 0;
                    assets.Add(new Property.Microservice.Models.PropertyAsset
                    {
                        TenantId = "TEN-001",
                        CompleteAddress = $"{i} {GetStreetName(i)}, {GetCity(i)}, South Africa",
                        ErfSpatialSize = $"{(220 + (i % 180))} m²",
                        TitleDeedType = i % 3 == 0 ? "Sectional Title" : "Freehold",
                        PortfolioZoning = i % 4 == 0 ? "Commercial Zone" : i % 3 == 0 ? "Residential Zone" : "Mixed Use Zone",
                        MarketClass = isRental ? "Rental" : "Sale",
                        PropertyTypeId = propertyType.PropertyTypeId,
                        ValuationPrice = 350000m + i * 18000,
                        SalePrice = isRental ? null : 325000m + i * 20000,
                        BaseRentalRate = isRental ? 6000m + i * 120 : null,
                        RentEscalationPercentage = isRental ? 7.5m + (i % 5) * 0.25m : null,
                        LeaseTermMonths = isRental ? 12 + (i % 20) * 6 : null,
                        TenantInstallationAllowance = isRental ? 25000m + i * 500 : 0,
                        Latitude = -33.9m + ((i % 100) * 0.01m),
                        Longitude = 18.4m + ((i % 70) * 0.02m),
                        VerificationDocumentsJson = "[]",
                        LinkedOwnerId = $"OT-{((i % 500) + 1):0000}",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.PropertyAssets.AddRange(assets);
            }

            context.SaveChanges();

            if (context.PropertyUnits.Count() < 2000)
            {
                var units = new List<Property.Microservice.Models.PropertyUnit>();
                var propertyIds = context.PropertyAssets.Select(p => p.PropertyId).ToList();

                foreach (var propertyId in propertyIds)
                {
                    var suiteCount = propertyId % 2 == 0 ? 2 : 3;
                    for (var offset = 1; offset <= suiteCount; offset++)
                    {
                        units.Add(new Property.Microservice.Models.PropertyUnit
                        {
                            PropertyId = propertyId,
                            UnitNumber = $"Unit {offset}",
                            UnitDescription = $"Suite {offset} in property {propertyId}",
                            NetRentableAreaSqM = 60 + (offset * 35),
                            SpecificUnitRentalPrice = 5500m + propertyId + offset * 250,
                            SpecificUnitSalePrice = 150000m + propertyId * 500 + offset * 1000,
                            UnitImagesJson = "[]",
                            IsCurrentlyVacant = offset % 2 == 0,
                            CreatedAt = DateTime.UtcNow.AddDays(-propertyId)
                        });
                    }
                }

                context.PropertyUnits.AddRange(units);
            }

            context.SaveChanges();

            if (context.PropertyPlatformSyndications.Count() < 1000)
            {
                var syndications = new List<Property.Microservice.Models.PropertyPlatformSyndication>();
                var propertyIds = context.PropertyAssets.Select(p => p.PropertyId).ToList();
                var platforms = context.ExternalPlatforms.Select(p => p.PlatformId).ToList();
                for (var i = context.PropertyPlatformSyndications.Count() + 1; i <= 1000; i++)
                {
                    var propertyId = propertyIds[(i - 1) % propertyIds.Count];
                    var platformId = platforms[(i - 1) % platforms.Count];
                    syndications.Add(new Property.Microservice.Models.PropertyPlatformSyndication
                    {
                        PropertyId = propertyId,
                        PlatformId = platformId,
                        IsSyndicatedActive = true,
                        ExternalReferenceIndex = $"EXT-{i:0000}",
                        LivePortalListingUrl = $"https://portal.example.com/listing/{i}",
                        LastSynchronizedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.PropertyPlatformSyndications.AddRange(syndications);
            }

            context.SaveChanges();
        }

        private static string GetStreetName(int index)
        {
            var names = new[] { "Harbour", "Oak", "Sunset", "Vine", "Market", "Main", "River", "Forest", "Hill", "Garden" };
            return names[index % names.Length] + " Street";
        }

        private static string GetCity(int index)
        {
            var cities = new[] { "Cape Town", "Johannesburg", "Durban", "Pretoria", "Sandton", "Citrusdal", "Gqeberha", "Bloemfontein" };
            return cities[index % cities.Length];
        }
    }
}
