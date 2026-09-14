using Microsoft.EntityFrameworkCore;
using Property.Microservice.DTOs;
using Property.Microservice.Models;

namespace Property.Microservice.Data
{
    public class PropertyDbContext : DbContext
    {
        public PropertyDbContext(DbContextOptions<PropertyDbContext> options) : base(options)
        {
        }

        // Register tracking entity lookups and data tables
        public DbSet<PropertyType> PropertyTypes { get; set; }
        public DbSet<PropertyAsset> PropertyAssets { get; set; }
        public DbSet<PropertyUnit> PropertyUnits { get; set; }
        public DbSet<ExternalPlatform> ExternalPlatforms { get; set; }
        public DbSet<PropertyPlatformSyndication> PropertyPlatformSyndications { get; set; }
        public DbSet<UnitLease> UnitLeases { get; set; }
        public DbSet<MaintenanceCall> MaintenanceCalls { get; set; }
        public DbSet<SoleMandate> SoleMandates { get; set; }
        public DbSet<AmenityType> AmenityTypes { get; set; }
        public DbSet<PropertyUnitAmenity> PropertyUnitAmenities { get; set; }
        public DbSet<ImportJob> ImportJobs { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Master-Detail relational cascading updates matching your multi-tenant units
            modelBuilder.Entity<PropertyUnit>()
                .HasOne(u => u.MasterPropertyDetails)
                .WithMany(p => p.SubUnits)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // A tenant user holds at most one active lease per unit — prevents duplicates
            // so a Tenant always resolves to a single leased unit (their current home).
            modelBuilder.Entity<UnitLease>()
                .HasIndex(l => new { l.UserId, l.PropertyUnitId, l.IsActive });

            // 🛡️ Sole-mandate: cascade-delete with the parent property, AND a
            // filtered unique index so the database itself enforces
            // "at most one active mandate per property" — even if two
            // clients race the POST /mandates endpoint.
            modelBuilder.Entity<SoleMandate>()
                .HasOne(m => m.Property)
                .WithMany(p => p.SoleMandates)
                .HasForeignKey(m => m.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🏠/🏢 Amenities: one instance of a given amenity per (property, unit-or-null) scope.
            // Two filtered unique indexes cover both scopes:
            //   - property-level (PropertyUnitId IS NULL)
            //   - unit-level     (PropertyUnitId IS NOT NULL)
            modelBuilder.Entity<PropertyUnitAmenity>()
                .HasIndex(a => new { a.PropertyId, a.AmenityTypeId })
                .IsUnique()
                .HasFilter("[PropertyUnitId] IS NULL");
            modelBuilder.Entity<PropertyUnitAmenity>()
                .HasIndex(a => new { a.PropertyId, a.PropertyUnitId, a.AmenityTypeId })
                .IsUnique()
                .HasFilter("[PropertyUnitId] IS NOT NULL");
            modelBuilder.Entity<PropertyUnitAmenity>()
                .HasOne(a => a.Property)
                .WithMany()
                .HasForeignKey(a => a.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
            // NO ACTION on the unit + type FKs to avoid the multiple-cascade-path
            // cycle that occurs when a property (and thus its units) is deleted.
            modelBuilder.Entity<PropertyUnitAmenity>()
                .HasOne(a => a.PropertyUnit)
                .WithMany()
                .HasForeignKey(a => a.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PropertyUnitAmenity>()
                .HasOne(a => a.AmenityType)
                .WithMany()
                .HasForeignKey(a => a.AmenityTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
