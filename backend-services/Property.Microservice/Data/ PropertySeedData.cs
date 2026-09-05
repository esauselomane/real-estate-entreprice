using System.Text.Json;

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

            // 📚 Seed the amenity catalogue the SuperAdmin workspace manages.
            if (!context.AmenityTypes.Any())
            {
                context.AmenityTypes.AddRange(
                    new Property.Microservice.Models.AmenityType { Name = "Parking",      Label = "Allocated parking bays", RequiresNumber = true, IsResidentialOnly = false },
                    new Property.Microservice.Models.AmenityType { Name = "PetsAllowed",  Label = "Pets allowed",          RequiresBoolean = true, IsResidentialOnly = true  },
                    new Property.Microservice.Models.AmenityType { Name = "Gym",          Label = "Gym",                   RequiresBoolean = true,  IsResidentialOnly = false },
                    new Property.Microservice.Models.AmenityType { Name = "SwimmingPool", Label = "Swimming pool",         RequiresBoolean = true,  IsResidentialOnly = false },
                    new Property.Microservice.Models.AmenityType { Name = "Security",     Label = "24/7 security",         RequiresBoolean = true,  IsResidentialOnly = false },
                    new Property.Microservice.Models.AmenityType { Name = "AirConditioning", Label = "Air conditioning", RequiresBoolean = true, IsResidentialOnly = false }
                );
            }

            context.SaveChanges();
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
                        // 🧬 Each property is stamped with a tenant matching the
                        // CRM roster (TEN-0001 … TEN-1000) so the TenantSwitcher
                        // can scope the property map / list to a single agency.
                        TenantId = $"TEN-{(i % 1000) + 1:0000}",
                        CompleteAddress = $"{i} {GetStreetName(i)}, {GetCityInfo(i).City}, South Africa",
                        ErfSpatialSize = $"{(220 + (i % 180))} m²",
                        TitleDeedType = i % 3 == 0 ? "Sectional Title" : "Freehold",
                        PortfolioZoning = i % 4 == 0 ? "Commercial Zone" : i % 3 == 0 ? "Residential Zone" : "Mixed Use Zone",
                        MarketClass = isRental ? "Rental" : "Sale",
                        IsForSale = isRental ? (i % 5 == 0) : true,   // every 5th rental unit is also marketed for sale
                        IsForRent = isRental || (i % 5 == 0),        // rentals + every 5th sale unit is also marketed for rent
                        PropertyTypeId = propertyType.PropertyTypeId,
                        ValuationPrice = 350000m + i * 18000,
                        SalePrice = isRental ? null : 325000m + i * 20000,
                        TenantInstallationAllowance = isRental ? 25000m + i * 500 : 0,
                        Latitude = GetCityInfo(i).Lat,
                        Longitude = GetCityInfo(i).Lon,
                        VerificationDocumentsJson = SamplePropertyImageRefs((i % 9) + 1),
                        LinkedOwnerId = $"OT-{((i % 500) + 1):0000}",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.PropertyAssets.AddRange(assets);
            }

            context.SaveChanges();

            // Backfill sample image references for any property rows whose gallery is still
            // empty, so the brochure builder always has photos to select from.
            var emptyProps = context.PropertyAssets
                .Where(p => p.VerificationDocumentsJson == null || p.VerificationDocumentsJson == "[]")
                .ToList();
            if (emptyProps.Any())
            {
                foreach (var p in emptyProps)
                    p.VerificationDocumentsJson = SamplePropertyImageRefs((p.PropertyId % 9) + 1);
                context.SaveChanges();
            }

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
                            RentalRate = 5000m + propertyId * 100 + offset * 300,
                            RentEscalationPercentage = 7.0m + (propertyId % 5) * 0.5m,
                            LeaseTermMonths = 12 + (propertyId % 24) * 3,
                            UnitImagesJson = SamplePropertyImageRefs(((propertyId + offset) % 6) + 1),
                            IsCurrentlyVacant = offset % 2 == 0,
                            CreatedAt = DateTime.UtcNow.AddDays(-propertyId)
                        });
                    }
                }

                context.PropertyUnits.AddRange(units);
            }

            context.SaveChanges();

            // Backfill sample unit image references for any unit rows whose gallery is empty.
            var emptyUnits = context.PropertyUnits
                .Where(u => u.UnitImagesJson == null || u.UnitImagesJson == "[]")
                .ToList();
            if (emptyUnits.Any())
            {
                foreach (var u in emptyUnits)
                    u.UnitImagesJson = SamplePropertyImageRefs((u.PropertyUnitId % 6) + 1);
                context.SaveChanges();
            }

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

            // 🌐 Ownership link: bind property #1 to the seeded Owner user so the
            // Owner role (UserId = USR-OWNER-01, TenantId = TEN-001) resolves "their
            // properties" via PropertyAsset.LinkedOwnerId == User.UserId. Idempotent.
            var ownerUnitFirstId = context.PropertyUnits
                .Where(u => u.PropertyId == 1)
                .OrderBy(u => u.PropertyUnitId)
                .Select(u => u.PropertyUnitId)
                .FirstOrDefault();

            var firstProperty = context.PropertyAssets.FirstOrDefault(p => p.PropertyId == 1);
            if (firstProperty != null && firstProperty.LinkedOwnerId != "USR-OWNER-01")
            {
                firstProperty.LinkedOwnerId = "USR-OWNER-01";
            }

            // 🏷️ Active lease: bind the seeded Tenant user (UserId = USR-TENANT-01,
            // TenantId = TEN-001) to unit #1 of property #1. This is the ONLY property
            // the Tenant sees; flipping IsActive -> false revokes their access.
            if (ownerUnitFirstId > 0)
            {
                var existingLease = context.UnitLeases
                    .FirstOrDefault(l => l.UserId == "USR-TENANT-01" && l.PropertyUnitId == ownerUnitFirstId);
                if (existingLease == null)
                {
                    context.UnitLeases.Add(new Property.Microservice.Models.UnitLease
                    {
                        UserId = "USR-TENANT-01",
                        TenantId = "TEN-001",
                        PropertyId = 1,
                        PropertyUnitId = ownerUnitFirstId,
                        LeaseStart = DateTime.UtcNow.AddDays(-30),
                        LeaseEnd = DateTime.UtcNow.AddDays(30),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingLease.IsActive = true;
                    existingLease.LeaseEnd = DateTime.UtcNow.AddDays(30);
                }

                // An active maintenance call so the Tenant has something to view.
                var existingCall = context.MaintenanceCalls
                    .FirstOrDefault(m => m.RequestedByUserId == "USR-TENANT-01" && m.PropertyUnitId == ownerUnitFirstId);
                if (existingCall == null)
                {
                    context.MaintenanceCalls.Add(new Property.Microservice.Models.MaintenanceCall
                    {
                        TenantId = "TEN-001",
                        RequestedByUserId = "USR-TENANT-01",
                        PropertyId = 1,
                        PropertyUnitId = ownerUnitFirstId,
                        Title = "Routine HVAC inspection requested",
                        Description = "Please schedule the annual HVAC service for the leased suite.",
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow.AddDays(-3),
                        UpdatedAt = DateTime.UtcNow.AddDays(-3)
                    });
                }
            }

            context.SaveChanges();

            // 🧬 One-shot backfill: re-stamp legacy "TEN-001" property rows
            // into the per-agency tenant distribution so the TenantSwitcher
            // actually narrows the property list. Idempotent: once no rows
            // carry TEN-001 the loop is a no-op.
            var legacyStale = context.PropertyAssets
                .Where(p => p.TenantId == "TEN-001" || p.TenantId == "")
                .ToList();
            foreach (var p in legacyStale)
            {
                var slot = (Math.Abs(p.PropertyId) % 1000) + 1;
                p.TenantId = $"TEN-{slot:0000}";
            }
            if (legacyStale.Count > 0) context.SaveChanges();
        }

        private static string GetStreetName(int index)
        {
            var names = new[] { "Harbour", "Oak", "Sunset", "Vine", "Market", "Main", "River", "Forest", "Hill", "Garden" };
            return names[index % names.Length] + " Street";
        }

        private static (string City, decimal Lat, decimal Lon) GetCityInfo(int index)
        {
            // 📍 South-African city centres — coords ring-fenced to SA and spread nationwide
            // (no longer clustered at a single point). The address city and the lat/lon stay
            // in sync because both are driven by the same index % cities.Length selector.
            var cities = new (string City, decimal Lat, decimal Lon)[]
            {
                ("Cape Town",    -33.9249m,  18.4207m),
                ("Johannesburg",  -26.2041m,  28.0479m),
                ("Durban",        -29.8599m,  31.0579m),
                ("Pretoria",      -25.7617m,  28.1881m),
                ("Sandton",       -26.1081m,  28.0567m),
                ("Citrusdal",     -32.5833m,  22.2833m),
                ("Gqeberha",      -33.9625m,  25.6250m),
                ("Bloemfontein",  -28.7465m,  26.1316m)
            };
            var c = cities[index % cities.Length];
            // Small ±5km jitter so pins scatter within each city region instead of overlapping.
            var lat = Math.Round(c.Lat + ((index * 7 % 11 - 5) * 0.01m), 6);
            var lon = Math.Round(c.Lon + ((index * 3 % 11 - 5) * 0.01m), 6);
            return (c.City, lat, lon);
        }

        private static string GetCity(int index)
        {
            return GetCityInfo(index).City;
        }

        // 📸 Sample photo references that stand-in for media-vault uploads so the brochure
        // builder always has existing photos for a broker to select from. A broker never
        // creates images here — they pick from these existing references.
        private static string SamplePropertyImageRefs(int seed)
        {
            var refs = new[]
            {
                $"exterior-day-{seed}.jpg",
                $"interior-living-{seed}.jpg",
                $"interior-kitchen-{seed}.jpg",
                $"garden-facing-{seed}.jpg"
            };
            return JsonSerializer.Serialize(refs);
        }
    }
}
