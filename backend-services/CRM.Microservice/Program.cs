using CRM.Microservice.Data;
using CRM.Microservice.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Register REST Controller frameworks
builder.Services.AddControllers();

// 🔐 JWT AUTHENTICATION (issued by the Identity microservice). Tenant is resolved
// from the bearer token claims rather than the URL — see GetCurrentTenant() in CRMController.
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// 🌐 Outbound HTTP client used by the lead→deal auto-matching engine to query the
// Property microservice through the API gateway (tenant scoping is enforced there).
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionCheckService>();


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

    // 🛠️ EnsureCreated() is a no-op once the database has any table, so tables that are
    // ever DROPPED (or created first on a fresh DB) must be recreated here — mirroring the
    // [OperationalRecords]/[GeographyRecords]/[ContactPersons] scripts below. CRMContacts
    // supports nullable company/legal-entity links so leads can be auto-promoted to deals
    // with buyer/seller contacts that are not bound to a company.

    // 🎯 Fix: Drop LeadRecords table if LeadStage column is int (pre-HasConversion<string> schema).
    // HasConversion<string>() requires an nvarchar column so EF Core generates WHERE [LeadStage] = N'Contacted'.
    // On databases created before the string conversion was added, the column remains int and CountAsync
    // with an enum filter returns the entire table instead of the filtered subset.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[LeadRecords]', N'U') IS NOT NULL
        BEGIN
            IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                WHERE c.object_id = OBJECT_ID(N'[LeadRecords]')
                AND c.name = N'LeadStage'
                AND ty.name = N'int'
            )
            BEGIN
                DROP TABLE [LeadRecords]
            END
        END
    ");

    // 🎯 Fix: Drop DealRecords table if DealStage column is int (pre-HasConversion<string> schema).
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[DealRecords]', N'U') IS NOT NULL
        BEGIN
            IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                WHERE c.object_id = OBJECT_ID(N'[DealRecords]')
                AND c.name = N'DealStage'
                AND ty.name = N'int'
            )
            BEGIN
                DROP TABLE [DealRecords]
            END
        END
    ");
    databaseContext.Database.EnsureCreated();
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[LeadRecords]', N'U') IS NOT NULL
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'[LeadRecords]')
                AND c.name = N'LeadType'
            )
            BEGIN
                ALTER TABLE [LeadRecords] ADD [LeadType] nvarchar(50) NOT NULL CONSTRAINT DF_LeadRecords_LeadType DEFAULT N'ForSale'
            END
        END
    ");
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[CRMContacts]', N'U') IS NULL
        BEGIN
            CREATE TABLE [CRMContacts] (
                [ContactId]              nvarchar(450) NOT NULL CONSTRAINT PK_CRMContacts PRIMARY KEY,
                [TenantId]               nvarchar(50)  NOT NULL,
                [FullName]               nvarchar(256) NOT NULL,
                [EmailAddress]           nvarchar(256) NOT NULL,
                [PhoneNumber]            nvarchar(64)  NULL,
                [PositionJobTitle]       nvarchar(128) NOT NULL CONSTRAINT DF_CRMContacts_Position DEFAULT N'Director',
                [ContactRoleType]        nvarchar(50)  NOT NULL CONSTRAINT DF_CRMContacts_Role DEFAULT N'Buyer',
                [AssociatedCompanyId]    nvarchar(450) NULL,
                [AssociatedLegalEntityId] nvarchar(450) NULL,
                [CreatedAt]              datetime2     NOT NULL CONSTRAINT DF_CRMContacts_CreatedAt DEFAULT GETUTCDATE()
            );
            ALTER TABLE [CRMContacts] ADD CONSTRAINT [FK_CRMContacts_CRMCompanies_AssociatedCompanyId]
                FOREIGN KEY ([AssociatedCompanyId]) REFERENCES [CRMCompanies] ([CompanyId]);
            ALTER TABLE [CRMContacts] ADD CONSTRAINT [FK_CRMContacts_LegalEntities_AssociatedLegalEntityId]
                FOREIGN KEY ([AssociatedLegalEntityId]) REFERENCES [CRMLegalEntities] ([LegalEntityId]);
            CREATE INDEX [IX_CRMContacts_TenantId] ON [CRMContacts] ([TenantId]);
        END
    ");

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

        -- 🛠️ Geography reference table (owner/tenant area-of-interest autopopulation).
        IF OBJECT_ID(N'[GeographyRecords]', N'U') IS NULL
        BEGIN
            CREATE TABLE [GeographyRecords] (
                [GeographyId] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [Country] nvarchar(128) NOT NULL DEFAULT N'South Africa',
                [Province] nvarchar(128) NOT NULL, [City] nvarchar(128) NOT NULL,
                [Suburb] nvarchar(128) NULL,
                [Latitude] decimal(10,6) NOT NULL, [Longitude] decimal(10,6) NOT NULL
            );
            CREATE INDEX [IX_GeographyRecords_Suburb] ON [GeographyRecords] ([Country], [Province], [City], [Suburb]);
        END

        -- 🛠️ Backfill owner/tenant interest columns onto pre-existing databases.
        IF COL_LENGTH(N'[OwnerTenantRecords]', 'PropertyTypeInterests') IS NULL
            ALTER TABLE [OwnerTenantRecords] ADD [PropertyTypeInterests] nvarchar(max) NOT NULL CONSTRAINT DF_OT_PropertyTypeInterests DEFAULT N'';
        IF COL_LENGTH(N'[OwnerTenantRecords]', 'AreaInterestIds') IS NULL
            ALTER TABLE [OwnerTenantRecords] ADD [AreaInterestIds] nvarchar(max) NOT NULL CONSTRAINT DF_OT_AreaInterestIds DEFAULT N'';

        -- 🛠️ Contact persons bound to agencies (promotable to AgencyAdmin broker profiles).
        IF OBJECT_ID(N'[ContactPersons]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ContactPersons] (
                [ContactPersonId] nvarchar(64) NOT NULL PRIMARY KEY,
                [AgencyId] nvarchar(64) NOT NULL, [FullName] nvarchar(256) NOT NULL,
                [Email] nvarchar(256) NOT NULL, [Phone] nvarchar(64) NULL,
                [Position] nvarchar(128) NULL, [Role] nvarchar(64) NOT NULL CONSTRAINT DF_CP_Role DEFAULT N'Owner',
                [IsAgencyAdmin] bit NOT NULL CONSTRAINT DF_CP_IsAgencyAdmin DEFAULT 0,
                [CreatedAt] datetime2 NOT NULL
            );
            CREATE INDEX [IX_ContactPersons_Agency] ON [ContactPersons] ([AgencyId]);
        END

        -- 🛠️ Agency trading / registration / logo fields.
        IF COL_LENGTH(N'[AgencyRecords]', 'TradingName') IS NULL
            ALTER TABLE [AgencyRecords] ADD [TradingName] nvarchar(256) NOT NULL CONSTRAINT DF_Agency_TradingName DEFAULT N'';
        IF COL_LENGTH(N'[AgencyRecords]', 'RegistrationNumber') IS NULL
            ALTER TABLE [AgencyRecords] ADD [RegistrationNumber] nvarchar(128) NOT NULL CONSTRAINT DF_Agency_RegNo DEFAULT N'';
        IF COL_LENGTH(N'[AgencyRecords]', 'CompanyLogoUrl') IS NULL
            ALTER TABLE [AgencyRecords] ADD [CompanyLogoUrl] nvarchar(512) NOT NULL CONSTRAINT DF_Agency_Logo DEFAULT N'';
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

    // 💰 Commission fields on leads + deals (realised commission only filled at ClosedWon).
    databaseContext.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH(N'[LeadRecords]', 'CommissionType') IS NULL
            ALTER TABLE [LeadRecords] ADD [CommissionType] nvarchar(20) NOT NULL CONSTRAINT DF_Lead_CommissionType DEFAULT N'Percent';
        IF COL_LENGTH(N'[LeadRecords]', 'CommissionValue') IS NULL
            ALTER TABLE [LeadRecords] ADD [CommissionValue] decimal(18,2) NOT NULL CONSTRAINT DF_Lead_CommissionValue DEFAULT 0;
        IF COL_LENGTH(N'[DealRecords]', 'CommissionType') IS NULL
            ALTER TABLE [DealRecords] ADD [CommissionType] nvarchar(20) NOT NULL CONSTRAINT DF_Deal_CommissionType DEFAULT N'Percent';
        IF COL_LENGTH(N'[DealRecords]', 'CommissionValue') IS NULL
            ALTER TABLE [DealRecords] ADD [CommissionValue] decimal(18,2) NOT NULL CONSTRAINT DF_Deal_CommissionValue DEFAULT 0;
        IF COL_LENGTH(N'[DealRecords]', 'RealisedCommission') IS NULL
            ALTER TABLE [DealRecords] ADD [RealisedCommission] decimal(18,2) NULL;
        -- 💱 Marker so the frontend can render an AUTO badge on deals where
        -- realised commission was filled by formula (CommissionType x TransactionValue)
        -- because the user left it blank/zero. Server is the source of truth.
        IF COL_LENGTH(N'[DealRecords]', 'RealisedCommissionIsAuto') IS NULL
            ALTER TABLE [DealRecords] ADD [RealisedCommissionIsAuto] bit NOT NULL CONSTRAINT DF_Deal_RealisedCommissionIsAuto DEFAULT 0;
        IF COL_LENGTH(N'[LeadRecords]', 'LinkedContactId') IS NULL
            ALTER TABLE [LeadRecords] ADD [LinkedContactId] nvarchar(50) NULL;
        -- 🧑‍💼 Multi-broker rename: legacy AllocatedToAgent (free-text) → AllocatedBrokerIds (comma-separated ids).
        -- Idempotent across reseeds: detect old column, rename; otherwise ensure new exists.
        IF COL_LENGTH(N'[LeadRecords]', 'AllocatedToAgent') IS NOT NULL AND COL_LENGTH(N'[LeadRecords]', 'AllocatedBrokerIds') IS NULL
            EXEC sp_rename N'[LeadRecords].[AllocatedToAgent]', N'AllocatedBrokerIds', N'COLUMN';
        IF COL_LENGTH(N'[LeadRecords]', 'AllocatedBrokerIds') IS NULL
            ALTER TABLE [LeadRecords] ADD [AllocatedBrokerIds] nvarchar(MAX) NOT NULL CONSTRAINT DF_Lead_AllocatedBrokerIds DEFAULT N'';
        IF COL_LENGTH(N'[DealRecords]', 'AllocatedToAgent') IS NOT NULL AND COL_LENGTH(N'[DealRecords]', 'AllocatedBrokerIds') IS NULL
            EXEC sp_rename N'[DealRecords].[AllocatedToAgent]', N'AllocatedBrokerIds', N'COLUMN';
        IF COL_LENGTH(N'[DealRecords]', 'AllocatedBrokerIds') IS NULL
            ALTER TABLE [DealRecords] ADD [AllocatedBrokerIds] nvarchar(MAX) NOT NULL CONSTRAINT DF_Deal_AllocatedBrokerIds DEFAULT N'';
    ");

    // 💱 One-time backfill: any ClosedWon deal with a falsy RealisedCommission
    // gets the formula value AND the IsAuto marker, so the dashboard, broker
    // leaderboard, and CRM reports don't return 0 for closed deals that have
    // a real commission value sitting in CommissionType × CommissionValue.
    // Idempotent — only touches rows still missing a realised value.
    databaseContext.Database.ExecuteSqlRaw(@"
        UPDATE d SET
            RealisedCommission = CASE
                WHEN d.CommissionType = N'Percent'
                    THEN ROUND(d.TransactionValue * d.CommissionValue / 100.0, 2)
                ELSE d.CommissionValue
            END,
            RealisedCommissionIsAuto = 1
        FROM [DealRecords] d
        WHERE d.DealStage = N'ClosedWon'
          AND (d.RealisedCommission IS NULL OR d.RealisedCommission = 0)
          AND d.CommissionValue > 0;
    ");

    // 🧾 Brochure card (cart) + per-line unit selection ledger for the Presentation stage.
    // The card is broker-scoped per deal; lines hold the curated sub-units (max 5) the
    // worker renders as disclosure sheets. Soft-deleted by the worker once the PDF lands.
    databaseContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[BrochureCards]', N'U') IS NULL
        BEGIN
            CREATE TABLE [BrochureCards] (
                [CardId]            int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [TenantId]          nvarchar(50)      NOT NULL,
                [DealId]            int               NOT NULL,
                [BrokerAgentId]     nvarchar(256)     NOT NULL,
                [ClientName]        nvarchar(256)     NOT NULL CONSTRAINT DF_BrochureCard_ClientName DEFAULT N'',
                [ClientEmail]       nvarchar(256)     NOT NULL CONSTRAINT DF_BrochureCard_ClientEmail DEFAULT N'',
                [ClientPhone]       nvarchar(64)      NOT NULL CONSTRAINT DF_BrochureCard_ClientPhone DEFAULT N'',
                [Status]            nvarchar(20)      NOT NULL CONSTRAINT DF_BrochureCard_Status DEFAULT N'Active',
                [IsDeleted]         bit               NOT NULL CONSTRAINT DF_BrochureCard_IsDeleted DEFAULT 0,
                [GeneratedFilePath] nvarchar(512)     NOT NULL CONSTRAINT DF_BrochureCard_FilePath DEFAULT N'',
                [CreatedAt]         datetime2         NOT NULL CONSTRAINT DF_BrochureCard_CreatedAt DEFAULT GETUTCDATE(),
                [UpdatedAt]         datetime2         NOT NULL CONSTRAINT DF_BrochureCard_UpdatedAt DEFAULT GETUTCDATE()
            );
            CREATE INDEX [IX_BrochureCards_Tenant_Deal] ON [BrochureCards] ([TenantId], [DealId]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_BrochureCards_Broker]    ON [BrochureCards] ([TenantId], [BrokerAgentId]) WHERE [IsDeleted] = 0 AND [Status] IN (N'Active', N'Generating');
        END

        IF OBJECT_ID(N'[BrochureCardLines]', N'U') IS NULL
        BEGIN
            CREATE TABLE [BrochureCardLines] (
                [LineId]             int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [CardId]             int               NOT NULL,
                [PropertyId]         int               NOT NULL,
                [UnitId]             int               NOT NULL,
                [UnitNumber]         nvarchar(50)      NOT NULL CONSTRAINT DF_BrochureLine_UnitNumber DEFAULT N'',
                [UnitDescription]    nvarchar(256)     NOT NULL CONSTRAINT DF_BrochureLine_UnitDesc DEFAULT N'',
                [NetRentableAreaSqM] decimal(10,2)     NOT NULL CONSTRAINT DF_BrochureLine_Area DEFAULT 0,
                [ImageRef]           nvarchar(512)     NOT NULL CONSTRAINT DF_BrochureLine_ImageRef DEFAULT N'',
                [IsDeleted]          bit               NOT NULL CONSTRAINT DF_BrochureLine_IsDeleted DEFAULT 0
            );
            CREATE INDEX [IX_BrochureCardLines_Card] ON [BrochureCardLines] ([CardId]) WHERE [IsDeleted] = 0;
            ALTER TABLE [BrochureCardLines] ADD CONSTRAINT [FK_BrochureCardLines_BrochureCards]
                FOREIGN KEY ([CardId]) REFERENCES [BrochureCards] ([CardId]);
        END
    ");

    CRMSeedData.EnsureSeeded(databaseContext);
}

// 5. Inject Pipeline Middleware routing and authorization components
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 6. Map public REST Controller endpoints
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "crm" }));

// 7. Start the service listener engine application
app.Run();
