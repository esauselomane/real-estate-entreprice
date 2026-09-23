using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;

namespace Property.Microservice.Services.SampleData;

internal static class SampleDataStorage
{
    public static void EnsureCreated(PropertyDbContext database)
    {
        if (!database.Database.IsSqlServer()) return;
        database.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('SampleDataReceipts') IS NULL
                CREATE TABLE SampleDataReceipts (TenantId nvarchar(64) PRIMARY KEY, ResultJson nvarchar(max) NOT NULL);
            """);
    }

    public static Task LockAsync(PropertyDbContext database, string tenantId, CancellationToken cancellationToken)
    {
        if (!database.Database.IsSqlServer()) return Task.CompletedTask;
        var resource = "sample-data:" + tenantId;
        return database.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @result < 0 THROW 50001, 'Sample data is being prepared.', 1;", cancellationToken);
    }
}
