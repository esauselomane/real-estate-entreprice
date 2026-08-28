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
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[OperationalRecords]', N'U') IS NULL
        BEGIN
            CREATE TABLE [OperationalRecords] (
                [OperationalRecordId] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [TenantId] nvarchar(128) NOT NULL, [Module] nvarchar(80) NOT NULL,
                [Title] nvarchar(256) NOT NULL, [PropertyReference] nvarchar(128) NOT NULL,
                [ContactName] nvarchar(256) NOT NULL, [Status] nvarchar(80) NOT NULL,
                [DueDate] datetime2 NULL, [Amount] decimal(18,2) NOT NULL,
                [Details] nvarchar(max) NOT NULL, [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NOT NULL
            );
            CREATE INDEX [IX_OperationalRecords_Tenant_Module] ON [OperationalRecords] ([TenantId], [Module]);
        END");
    CRMSeedData.EnsureSeeded(databaseContext);
}

// 5. Inject Pipeline Middleware routing and authorization components
app.UseRouting();
app.UseAuthorization();

// 6. Map public REST Controller endpoints
app.MapControllers();

// 7. Start the service listener engine application
app.Run();
