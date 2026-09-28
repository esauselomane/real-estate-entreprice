using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data;

/// <summary>Idempotent schema upgrade for the application's existing raw-DDL bootstrap.</summary>
public static class PaymentSchemaUpgrade
{
    public static async Task ApplyAsync(IdentityDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('BillingNotifications') IS NULL
                CREATE TABLE BillingNotifications (
                    Id int IDENTITY PRIMARY KEY, EventKey nvarchar(200) NOT NULL UNIQUE,
                    Recipient nvarchar(max) NOT NULL, Template nvarchar(max) NOT NULL,
                    CreatedAt datetime2 NOT NULL, SentAt datetime2 NULL, NextAttemptAt datetime2 NULL,
                    Attempts int NOT NULL, Error nvarchar(max) NULL);
            IF COL_LENGTH('TenantAgencies', 'TrialExtensionAppliedAt') IS NULL
                ALTER TABLE TenantAgencies ADD TrialExtensionAppliedAt datetime2 NULL;
            IF COL_LENGTH('Subscriptions', 'IsOnceOff') IS NULL
                ALTER TABLE Subscriptions ADD IsOnceOff bit NOT NULL CONSTRAINT DF_Subscriptions_IsOnceOff DEFAULT 0;
            IF COL_LENGTH('Subscriptions', 'Gateway') IS NULL
                ALTER TABLE Subscriptions ADD Gateway int NULL;
            IF COL_LENGTH('PaymentIntents', 'Gateway') IS NULL
                ALTER TABLE PaymentIntents ADD Gateway int NULL;
            IF COL_LENGTH('PaymentIntents', 'CheckoutResponseJson') IS NULL
                ALTER TABLE PaymentIntents ADD CheckoutResponseJson nvarchar(max) NULL;
            IF COL_LENGTH('PaymentIntents', 'CheckoutExpiresAt') IS NULL
                ALTER TABLE PaymentIntents ADD CheckoutExpiresAt datetime2 NULL;
            IF COL_LENGTH('WebhookEvents', 'Gateway') IS NULL
                ALTER TABLE WebhookEvents ADD Gateway int NULL;
            IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'AK_Subscriptions_TenantId')
                ALTER TABLE Subscriptions DROP CONSTRAINT AK_Subscriptions_TenantId;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'IX_Subscriptions_TenantId' AND is_unique = 1)
                DROP INDEX IX_Subscriptions_TenantId ON Subscriptions;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'IX_Subscriptions_TenantId')
                CREATE INDEX IX_Subscriptions_TenantId ON Subscriptions(TenantId);
            """);
    }
}
