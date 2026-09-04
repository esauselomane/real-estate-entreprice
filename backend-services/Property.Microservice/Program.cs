using Property.Microservice.Data;
using Property.Microservice.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTER CORE SYSTEM ROUTINGS
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 2. CONFIGURE MICROSOFT SQL SERVER ENTERPRISE DATABASE ENGINE POOL CONNECTION
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PropertyDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 3. 🛡️ REGISTER JWT BEARER AUTHENTICATION (mirrors Identity.Microservice signing keys)
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Identity.Microservice";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RealEstateApp.Client";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddAuthorization();

// 4. INJECT EVENT-DRIVEN KAFKA STREAMING SERVICE AS A SINGLETON
builder.Services.AddSingleton<KafkaPublisherService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionCheckService>();

var app = builder.Build();
// 5. AUTOMATED DATABASE LIFECYCLE RECOVERY & DATA SEEDING LOOPS
using (var serviceScope = app.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<PropertyDbContext>();
    databaseContext.Database.EnsureCreated();

    // 🛠️ Idempotent migration: add the unit-level document vault column to existing
    // PropertyUnits tables created before this field existed.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH(N'[PropertyUnits]', 'UnitDocumentsJson') IS NULL
        BEGIN
            ALTER TABLE [PropertyUnits] ADD [UnitDocumentsJson] nvarchar(max) NOT NULL
                CONSTRAINT [DF_PropertyUnits_UnitDocumentsJson] DEFAULT '[]';
        END
    ");

    // 🛠️ Widen UnitNumber from nvarchar(20) -> nvarchar(50) so realistic unit identifiers
    // like 'Suite 101 - Renovated' are editable without truncating server-side.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PropertyUnits]') AND name = N'UnitNumber' AND (max_length < 50 OR max_length = -1))
            ALTER TABLE [PropertyUnits] ALTER COLUMN [UnitNumber] nvarchar(50) NOT NULL;
    ");

    // 🛠️ Idempotent schema additions for the tenant lease ledger and maintenance log.
    // EnsureCreated() is a no-op against an existing database, so new tables must be
    // created here (mirroring the UnitDocumentsJson column migration above).
    databaseContext.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[UnitLeases]') AND type = N'U')
        BEGIN
            CREATE TABLE [UnitLeases] (
                [UnitLeaseId]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [UserId]             NVARCHAR(50)   NOT NULL,
                [TenantId]           NVARCHAR(50)   NOT NULL,
                [PropertyId]         INT            NOT NULL,
                [PropertyUnitId]     INT            NOT NULL,
                [LeaseStart]         DATETIME2      NOT NULL,
                [LeaseEnd]           DATETIME2      NOT NULL,
                [IsActive]           BIT            NOT NULL DEFAULT 1,
                [CreatedAt]          DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
                INDEX [IX_UnitLeases_UserId_PropertyUnitId_IsActive] ([UserId], [PropertyUnitId], [IsActive])
            );
        END
    ");

    // 📋 Lease enrichment: add the columns backing the SuperAdmin Lease tab.
    // Idempotent — every IF COL_LENGTH check is independent. Safe to re-run.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH(N'[UnitLeases]', N'ContactId') IS NULL
            ALTER TABLE [UnitLeases] ADD [ContactId] NVARCHAR(50) NULL;
        IF COL_LENGTH(N'[UnitLeases]', N'MonthlyRent') IS NULL
            ALTER TABLE [UnitLeases] ADD [MonthlyRent] DECIMAL(18,2) NULL;
        IF COL_LENGTH(N'[UnitLeases]', N'DepositAmount') IS NULL
            ALTER TABLE [UnitLeases] ADD [DepositAmount] DECIMAL(18,2) NULL;
        IF COL_LENGTH(N'[UnitLeases]', N'Notes') IS NULL
            ALTER TABLE [UnitLeases] ADD [Notes] NVARCHAR(MAX) NULL;
        IF COL_LENGTH(N'[UnitLeases]', N'AccruedInterestPercent') IS NULL
            ALTER TABLE [UnitLeases] ADD [AccruedInterestPercent] DECIMAL(5,2) NULL;
        IF COL_LENGTH(N'[UnitLeases]', N'TenantNameSnapshot') IS NULL
            ALTER TABLE [UnitLeases] ADD [TenantNameSnapshot] NVARCHAR(200) NULL;
    ");
    databaseContext.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[MaintenanceCalls]') AND type = N'U')
        BEGIN
            CREATE TABLE [MaintenanceCalls] (
                [MaintenanceCallId]  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [TenantId]           NVARCHAR(50)   NOT NULL,
                [RequestedByUserId]  NVARCHAR(50)   NOT NULL,
                [PropertyId]         INT            NOT NULL,
                [PropertyUnitId]     INT            NOT NULL,
                [Title]              NVARCHAR(250)  NOT NULL DEFAULT '',
                [Description]        NVARCHAR(MAX)  NOT NULL DEFAULT '',
                [Status]             NVARCHAR(50)   NOT NULL DEFAULT 'Open',
                [DueDate]            DATETIME2      NULL,
                [CreatedAt]          DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]          DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
                INDEX [IX_MaintenanceCalls_PropertyUnitId] ([PropertyUnitId])
            );
        END
    ");

    // 📜 Sole-mandate history. One property can have many SoleMandates,
    // but only ONE active at a time — enforced at the DB level via a
    // filtered unique index on (PropertyId) WHERE IsActive = 1.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[SoleMandates]') AND type = N'U')
        BEGIN
            CREATE TABLE [SoleMandates] (
                [SoleMandateId]     int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [PropertyId]        int             NOT NULL,
                [TenantId]          nvarchar(50)    NOT NULL,
                [StartDate]         datetime2       NOT NULL,
                [EndDate]           datetime2       NOT NULL,
                [IsActive]          bit             NOT NULL CONSTRAINT DF_SM_IsActive DEFAULT 1,
                [ClosedAt]          datetime2       NULL,
                [CreatedByUserId]   nvarchar(100)   NOT NULL CONSTRAINT DF_SM_CreatedBy DEFAULT N'',
                [CreatedAt]         datetime2       NOT NULL CONSTRAINT DF_SM_CreatedAt DEFAULT SYSUTCDATETIME(),
                CONSTRAINT [FK_SoleMandates_PropertyAssets] FOREIGN KEY ([PropertyId])
                    REFERENCES [PropertyAssets]([PropertyId]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_SoleMandates_PropertyId_IsActive] ON [SoleMandates] ([PropertyId], [IsActive]);
            CREATE UNIQUE INDEX [UX_SoleMandates_OneActivePerProperty] ON [SoleMandates] ([PropertyId]) WHERE [IsActive] = 1;
        END
    ");

    // 📈 New listing-eligibility flags: an asset may be marketed for SALE
    // and/or RENT simultaneously. Backfilled with safe defaults onto pre-existing DBs.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH(N'[PropertyAssets]', 'IsForSale') IS NULL
            ALTER TABLE [PropertyAssets] ADD [IsForSale] BIT NOT NULL CONSTRAINT DF_PA_IsForSale DEFAULT 1;
        IF COL_LENGTH(N'[PropertyAssets]', 'IsForRent') IS NULL
            ALTER TABLE [PropertyAssets] ADD [IsForRent] BIT NOT NULL CONSTRAINT DF_PA_IsForRent DEFAULT 0;
        -- Distribute flags across the seeded assets (odd = sale, even = rent, every 5th = both)
        UPDATE [PropertyAssets] SET
            IsForSale = CASE WHEN ([PropertyId] % 2 = 1 OR [PropertyId] % 5 = 0) THEN 1 ELSE 0 END,
            IsForRent = CASE WHEN ([PropertyId] % 2 = 0 OR [PropertyId] % 5 = 0) THEN 1 ELSE 0 END;
    ");

    // 📍 Ring-fence & spread geocoordinates across real South-African city centres (synced
    // to the address city via PropertyId % 8) with a small jitter. Runs every startup;
    // idempotent (recomputes the same value). Existing clustered coords are corrected here.
    databaseContext.Database.ExecuteSqlRaw(@"
        UPDATE [pa] SET
            Latitude  = c.La + (CAST([pa].[PropertyId] AS bigint) * 7 % 11 - 5) * 0.01,
            Longitude = c.Lo + (CAST([pa].[PropertyId] AS bigint) * 3 % 11 - 5) * 0.01
        FROM [PropertyAssets] AS [pa]
        INNER JOIN (VALUES
            (0, -33.9249, 18.4207), (1, -26.2041, 28.0479), (2, -29.8599, 31.0579),
            (3, -25.7617, 28.1881), (4, -26.1081, 28.0567), (5, -32.5833, 22.2833),
            (6, -33.9625, 25.6250), (7, -28.7465, 26.1316)
        ) AS c(n, La, Lo) ON c.n = [pa].[PropertyId] % 8;
    ");

    PropertySeedData.EnsureSeeded(databaseContext);
}

// 6. INJECT REQUEST PIPELINE MIDDLEWARE HANDLERS
//    Authentication MUST precede authorization so the principal is populated first.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 7. MAP TARGET REST API ENDPOINT CONTROLLERS
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "property" }));

// 8. START THE MICROSERVICE WORKSPACE ENGINE LISTENER
app.Run();
