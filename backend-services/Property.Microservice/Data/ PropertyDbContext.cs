using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Master-Detail relational cascading updates matching your multi-tenant units
            modelBuilder.Entity<PropertyUnit>()
                .HasOne(u => u.MasterPropertyDetails)
                .WithMany(p => p.SubUnits)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
