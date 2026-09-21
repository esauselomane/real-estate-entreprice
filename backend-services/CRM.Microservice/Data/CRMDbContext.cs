using System.Linq.Expressions;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Data
{
    public partial class CRMDbContext : DbContext
    {
        private static ContactType ParseContactType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ContactType.None;

            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            ContactType result = ContactType.None;
            foreach (var part in parts)
            {
                if (Enum.TryParse<ContactType>(part, true, out var parsed))
                    result |= parsed;
            }
            return result;
        }

        public CRMDbContext(DbContextOptions<CRMDbContext> options) : base(options)
        {
        }

        public DbSet<CRMCompany> CRMCompanies { get; set; }
        public DbSet<AgencyRecord> AgencyRecords { get; set; }
        public DbSet<BrokerRecord> BrokerRecords { get; set; }
        public DbSet<OwnerTenantRecord> OwnerTenantRecords { get; set; }
        public DbSet<CRMLegalEntity> CRMLegalEntities { get; set; }
        public DbSet<CRMContact> CRMContacts { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<LeadRecord> LeadRecords { get; set; }
        public DbSet<DealRecord> DealRecords { get; set; }
        public DbSet<OperationalRecord> OperationalRecords { get; set; }
        public DbSet<GeographyRecord> GeographyRecords { get; set; }
        public DbSet<ContactPerson> ContactPersons { get; set; }
        public DbSet<BrochureCard> BrochureCards { get; set; } = null!;
        public DbSet<BrochureCardLine> BrochureCardLines { get; set; } = null!;
        public DbSet<DealBrokerSplit> DealBrokerSplits { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<BrokerMonthlyTarget> BrokerMonthlyTargets { get; set; } = null!;
    public DbSet<BrokerPerformanceMetrics> BrokerPerformanceMetrics { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LeadRecord>()
                .Property(l => l.LeadStage)
                .HasConversion<string>();

            modelBuilder.Entity<DealRecord>()
                .Property(d => d.DealStage)
                .HasConversion<string>();

            modelBuilder.Entity<CRMContact>()
                .HasOne(c => c.CompanyDetails)
                .WithMany()
                .HasForeignKey(c => c.AssociatedCompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CRMContact>()
                .HasOne(c => c.LegalEntityDetails)
                .WithMany()
                .HasForeignKey(c => c.AssociatedLegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CRMLegalEntity>()
                .HasOne(l => l.CompanyDetails)
                .WithMany()
                .HasForeignKey(l => l.AssociatedCompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contact>()
                .Property(c => c.ContactType)
                .HasConversion(
                    v => v.ToString(),
                    v => ParseContactType(v));

            modelBuilder.Entity<Contact>()
                .HasOne(c => c.CompanyDetails)
                .WithMany()
                .HasForeignKey(c => c.AssociatedCompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contact>()
                .HasOne(c => c.LegalEntityDetails)
                .WithMany()
                .HasForeignKey(c => c.AssociatedLegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contact>()
                .HasOne(c => c.Agency)
                .WithMany(a => a.Contacts)
                .HasForeignKey(c => c.AgencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContactPerson>()
                .HasOne(c => c.Agency)
                .WithMany(a => a.ContactPersons)
                .HasForeignKey(c => c.AgencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BrochureCard>()
                .Property(c => c.Status)
                .HasConversion<string>();

            modelBuilder.Entity<BrochureCard>()
                .HasMany(c => c.Lines)
                .WithOne(l => l.Card)
                .HasForeignKey(l => l.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DealBrokerSplit>()
                .HasOne(s => s.Deal)
                .WithMany(d => d.BrokerSplits)
                .HasForeignKey(s => s.DealId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🛎️ Per-user inbox: index by (UserId, IsRead, CreatedAt) so
            // the bell can list unread-then-recent cheaply.
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.TenantId, n.CreatedAt });
        }
    }
}
