using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Models;

namespace Identity.Microservice.Data
{
    // 🗄️ Standard core framework inheritance mapping configuration
    public class IdentityDbContext : DbContext
    {
        public IdentityDbContext(){}
        public IdentityDbContext(DbContextOptions options) : base(options)
        {
        }

        // Map data models directly to real database structural tables
        public DbSet<TenantAgency> TenantAgencies { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<UserAccessClaim> UserAccessClaims { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enforce unique structural email parameters inside the user table
            modelBuilder.Entity<UserAccount>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
