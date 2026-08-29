using Microsoft.EntityFrameworkCore;
using CRM.Microservice.Models;

namespace CRM.Microservice.Data
{
    public class CRMDbContext : DbContext
    {
        public CRMDbContext(DbContextOptions<CRMDbContext> options) : base(options)
        {
        }

        public DbSet<CRMCompany> CRMCompanies { get; set; }
        public DbSet<AgencyRecord> AgencyRecords { get; set; }
        public DbSet<BrokerRecord> BrokerRecords { get; set; }
        public DbSet<OwnerTenantRecord> OwnerTenantRecords { get; set; }
        public DbSet<CRMLegalEntity> CRMLegalEntities { get; set; }
        public DbSet<CRMContact> CRMContacts { get; set; }
        public DbSet<LeadRecord> LeadRecords { get; set; }
        public DbSet<DealRecord> DealRecords { get; set; }
        public DbSet<OperationalRecord> OperationalRecords { get; set; }
        public DbSet<GeographyRecord> GeographyRecords { get; set; }
        public DbSet<ContactPerson> ContactPersons { get; set; }

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

            modelBuilder.Entity<ContactPerson>()
                .HasOne(c => c.Agency)
                .WithMany(a => a.ContactPersons)
                .HasForeignKey(c => c.AgencyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
