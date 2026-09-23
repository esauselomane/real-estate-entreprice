using Microsoft.EntityFrameworkCore;
using CRM.Microservice.Data;

namespace CRM.Microservice.Services.SampleData;

internal static class SampleDataStorage
{
    internal const string SchemaSql = """
        IF COL_LENGTH('DealRecords', 'SourceLeadId') IS NULL
            ALTER TABLE DealRecords ADD SourceLeadId int NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DealRecords_LeadRecords_SourceLeadId')
            ALTER TABLE DealRecords ADD CONSTRAINT FK_DealRecords_LeadRecords_SourceLeadId
                FOREIGN KEY (SourceLeadId) REFERENCES LeadRecords(LeadId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DealRecords_SourceLeadId' AND object_id = OBJECT_ID('DealRecords'))
            CREATE INDEX IX_DealRecords_SourceLeadId ON DealRecords(SourceLeadId);
        IF OBJECT_ID('SampleDataReceipts') IS NULL
            CREATE TABLE SampleDataReceipts (TenantId nvarchar(64) PRIMARY KEY, ResultJson nvarchar(max) NOT NULL);
        """;

    public static void EnsureCreated(CRMDbContext database)
    {
        if (database.Database.IsSqlServer()) database.Database.ExecuteSqlRaw(SchemaSql);
    }

    public static Task LockAsync(CRMDbContext database, string tenantId, CancellationToken cancellationToken)
    {
        if (!database.Database.IsSqlServer()) return Task.CompletedTask;
        var resource = "sample-data:" + tenantId;
        return database.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @result < 0 THROW 50001, 'Sample data is being prepared.', 1;", cancellationToken);
    }
}
