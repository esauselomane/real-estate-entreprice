using Microsoft.EntityFrameworkCore;
using Property.Microservice.Models;

namespace Developer.API.Data
{
    public class PropertyReadDbContext : DbContext
    {
        public PropertyReadDbContext(DbContextOptions<PropertyReadDbContext> options) : base(options) { }

        public DbSet<PropertyAsset> PropertyAssets { get; set; }
        public DbSet<PropertyUnit> PropertyUnits { get; set; }
        public DbSet<PropertyType> PropertyTypes { get; set; }
        public DbSet<PropertyPlatformSyndication> PropertyPlatformSyndications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PropertyAsset>()
                .HasIndex(p => p.TenantId);
        }
    }
}
