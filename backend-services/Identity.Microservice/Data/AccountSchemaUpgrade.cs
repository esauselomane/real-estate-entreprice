using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data;

/// <summary>Adds account recovery storage using the existing idempotent bootstrap pattern.</summary>
public static class AccountSchemaUpgrade
{
    public static Task ApplyAsync(IdentityDbContext database) => database.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID('PasswordRecoveryTokens') IS NULL
            CREATE TABLE PasswordRecoveryTokens (
                TokenHash nvarchar(64) PRIMARY KEY, UserId nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, ExpiresAt datetime2 NOT NULL);
        """);
}
