using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data;

public static class SampleDataSchema
{
    public static void EnsureCreated(IdentityDbContext database)
    {
        if (!database.Database.IsSqlServer()) return;
        database.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('SampleDataJobs') IS NULL
                CREATE TABLE SampleDataJobs (
                    TenantId nvarchar(64) PRIMARY KEY, State nvarchar(max) NOT NULL,
                    PlanJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL,
                    AvailableAt datetime2 NOT NULL, Attempts int NOT NULL, Error nvarchar(max) NULL,
                    ApprovedBy nvarchar(max) NULL, ProvisionedAt datetime2 NULL, NotifiedAt datetime2 NULL);
            IF COL_LENGTH('SampleDataJobs', 'ApprovedBy') IS NULL
                ALTER TABLE SampleDataJobs ADD ApprovedBy nvarchar(max) NULL, ProvisionedAt datetime2 NULL, NotifiedAt datetime2 NULL;
            """);
    }
}
