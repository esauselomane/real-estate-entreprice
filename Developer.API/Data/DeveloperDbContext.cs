using System.Security.Cryptography;
using System.Text;
using Developer.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Developer.API.Data
{
    public class DeveloperDbContext : DbContext
    {
        public DeveloperDbContext(DbContextOptions<DeveloperDbContext> options) : base(options) { }

        public DbSet<PublicOwnerAccount> PublicOwnerAccounts { get; set; }
        public DbSet<PublicOwnerSession> PublicOwnerSessions { get; set; }

        public DbSet<AgencyApiKey> AgencyApiKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PublicOwnerAccount>().HasIndex(owner => new { owner.AgencyId, owner.Email }).IsUnique();

            modelBuilder.Entity<AgencyApiKey>()
                .HasIndex(k => k.KeyHash)
                .IsUnique();

            modelBuilder.Entity<AgencyApiKey>()
                .HasIndex(k => new { k.AgencyId, k.IsRevoked });
        }
    }

    public static class ApiKeyGenerator
    {
        public static string GenerateKey()
        {
            return "bsc_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        public static string HashKey(string key)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static string GetPrefix(string key)
        {
            if (key.Length < 8) return key;
            return key.Substring(0, 8);
        }
    }
}
