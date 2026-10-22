using Microsoft.EntityFrameworkCore;

namespace Developer.API.Data;

public static class PublicOwnerSchema
{
    public static void EnsureCreated(DeveloperDbContext database)
    {
        database.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('PublicOwnerAccounts', 'U') IS NULL
            BEGIN
                CREATE TABLE PublicOwnerAccounts (
                    Id nvarchar(100) NOT NULL PRIMARY KEY, AgencyId nvarchar(100) NOT NULL,
                    Email nvarchar(254) NOT NULL, Name nvarchar(200) NOT NULL,
                    Phone nvarchar(40) NOT NULL, PasswordHash nvarchar(max) NOT NULL);
                CREATE UNIQUE INDEX IX_PublicOwnerAccounts_AgencyId_Email ON PublicOwnerAccounts(AgencyId, Email);
            END;
            IF OBJECT_ID('PublicOwnerSessions', 'U') IS NULL
                CREATE TABLE PublicOwnerSessions (
                    Id nvarchar(100) NOT NULL PRIMARY KEY, OwnerId nvarchar(100) NOT NULL,
                    AgencyId nvarchar(100) NOT NULL, ExpiresAt datetime2 NOT NULL);
            """);
    }
}
