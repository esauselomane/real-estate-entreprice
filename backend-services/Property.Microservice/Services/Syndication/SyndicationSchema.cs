using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;

namespace Property.Microservice.Services.Syndication;

/// <summary>Additive schema bootstrap matching the application's existing SQL Server deployment convention.</summary>
public static class SyndicationSchema
{
    public static void EnsureCreated(PropertyDbContext database)
    {
        if (!database.Database.IsSqlServer()) return;
        database.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'SyndicationConnections', N'U') IS NULL
            BEGIN
                CREATE TABLE SyndicationConnections (
                    Id int IDENTITY PRIMARY KEY, TenantId nvarchar(100) NOT NULL,
                    ProviderKey nvarchar(100) NOT NULL, Environment nvarchar(20) NOT NULL,
                    BranchId nvarchar(max) NOT NULL, AgentId nvarchar(max) NOT NULL, SettingsJson nvarchar(max) NOT NULL, Enabled bit NOT NULL,
                    CONSTRAINT UX_SyndicationConnection UNIQUE(TenantId, ProviderKey, Environment));
            END;
            IF COL_LENGTH('SyndicationConnections', 'ProtectedApiKey') IS NULL
                ALTER TABLE SyndicationConnections ADD ProtectedApiKey nvarchar(max) NOT NULL DEFAULT N'';
            IF OBJECT_ID(N'SyndicationPublications', N'U') IS NULL
            BEGIN
                CREATE TABLE SyndicationPublications (
                    Id int IDENTITY PRIMARY KEY, PropertyId int NOT NULL REFERENCES PropertyAssets(PropertyId),
                    ConnectionId int NOT NULL REFERENCES SyndicationConnections(Id), TenantId nvarchar(max) NOT NULL,
                    DesiredPublished bit NOT NULL, ConfirmedPublished bit NOT NULL, Revision int NOT NULL,
                    State nvarchar(max) NOT NULL, ExternalId nvarchar(max) NOT NULL, PublicUrl nvarchar(max) NOT NULL,
                    Error nvarchar(max) NULL, UpdatedAt datetime2 NOT NULL, LastConfirmedAt datetime2 NULL,
                    CONSTRAINT UX_SyndicationPublication UNIQUE(PropertyId, ConnectionId));
            END;
            IF OBJECT_ID(N'SyndicationCommands', N'U') IS NULL
            BEGIN
                CREATE TABLE SyndicationCommands (
                    Id int IDENTITY PRIMARY KEY, PublicationId int NOT NULL REFERENCES SyndicationPublications(Id),
                    Revision int NOT NULL, Operation nvarchar(max) NOT NULL, SnapshotJson nvarchar(max) NOT NULL,
                    RequestKey nvarchar(max) NOT NULL, State nvarchar(40) NOT NULL, Attempts int NOT NULL,
                    AvailableAt datetime2 NOT NULL, Error nvarchar(max) NULL);
                CREATE INDEX IX_SyndicationCommand_Pending ON SyndicationCommands(State, AvailableAt);
            END;
            """);
    }
}
