using CRM.Microservice.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Register REST Controller frameworks
builder.Services.AddControllers();

// 2. Configure Microsoft SQL Server Enterprise Database Engine Pool Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CRMDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();
// 4. Ensure the database layout matches mappings and seed base data parameters cleanly during startup loops
using (var serviceScope = app.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<CRMDbContext>();
    databaseContext.Database.EnsureCreated();

    // Batch 1: create the operational ledger table (for fresh databases) and
    // idempotently backfill the unit-linkage columns onto existing databases.
    // Note: the ALTER TABLE ADD statements are valid on their own, so this whole
    // batch compiles fine even when the columns do not yet exist.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[OperationalRecords]', N'U') IS NULL
        BEGIN
            CREATE TABLE [OperationalRecords] (
                [OperationalRecordId] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [TenantId] nvarchar(128) NOT NULL, [Module] nvarchar(80) NOT NULL,
                [Title] nvarchar(256) NOT NULL, [PropertyReference] nvarchar(128) NOT NULL,
                [ContactName] nvarchar(256) NOT NULL, [Status] nvarchar(80) NOT NULL,
                [DueDate] datetime2 NULL, [Amount] decimal(18,2) NOT NULL,
                [Details] nvarchar(max) NOT NULL, [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NOT NULL,
                [PropertyId] int NULL, [PropertyUnitId] int NULL
            );
            CREATE INDEX [IX_OperationalRecords_Tenant_Module] ON [OperationalRecords] ([TenantId], [Module]);
        END

        -- 🛠️ Idempotent migration: backfill the unit-linkage columns onto existing
        -- databases where the table was created before the property/unit linkage existed.
        IF COL_LENGTH(N'[OperationalRecords]', 'PropertyId') IS NULL
            ALTER TABLE [OperationalRecords] ADD [PropertyId] int NULL;
        IF COL_LENGTH(N'[OperationalRecords]', 'PropertyUnitId') IS NULL
            ALTER TABLE [OperationalRecords] ADD [PropertyUnitId] int NULL;
    ");

    // Batch 2: filtered index referencing [PropertyId]. Issued in its OWN batch so it
    // compiles against the schema AFTER the ALTER TABLE backfill above has committed
    // (SQL Server compiles a whole ExecuteSqlRaw batch up-front, so referencing a
    // column added by an earlier statement in the same batch raises 'Invalid column
    // name' before any statement executes).
    databaseContext.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[OperationalRecords]') AND name = N'IX_OperationalRecords_Property_Unit')
            CREATE INDEX [IX_OperationalRecords_Property_Unit] ON [OperationalRecords] ([PropertyId], [PropertyUnitId])
            WHERE [PropertyId] IS NOT NULL;
    ");

    CRMSeedData.EnsureSeeded(databaseContext);
}

// 5. Inject Pipeline Middleware routing and authorization components
app.UseRouting();
app.UseAuthorization();

// 6. Map public REST Controller endpoints
app.MapControllers();

// 7. Start the service listener engine application
app.Run();
