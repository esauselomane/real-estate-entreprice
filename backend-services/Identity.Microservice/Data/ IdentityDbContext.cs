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
        public DbSet<SystemRoleDefinition> SystemRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<LeadCaptureRequest> LeadCaptureRequests { get; set; }
        public DbSet<LeadWorkflow> LeadWorkflows { get; set; }
        public DbSet<PricingPackage> PricingPackages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enforce unique structural email parameters inside the user table
            modelBuilder.Entity<UserAccount>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleDefId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleDefId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Key)
                .IsUnique();
        }
    }
}
