/*
    CRM Seed Data - SQL Server
    Generated from CRMSeedData C# seeder.

    IMPORTANT:
    - This script assumes SQL Server and the table/column names shown below.
    - Verify column names and enum storage (string vs integer) against your EF models.
    - The C# seeder uses repository/entity mappings not included in the uploaded file.
    - CreatedAt/Trial dates use SQL Server UTC functions.
    - This script is designed to be re-runnable for generated seed IDs.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [RealEstateCRMDB];
GO

BEGIN TRANSACTION;

BEGIN TRY

/* ============================================================
   1. Schema backfill
   ============================================================ */

IF COL_LENGTH('CRMContacts', 'LinkedDealIds') IS NULL
    ALTER TABLE [CRMContacts] ADD [LinkedDealIds] nvarchar(max) NULL CONSTRAINT DF_CRMContacts_LinkedDealIds DEFAULT '';

IF COL_LENGTH('CRMContacts', 'Area') IS NULL
    ALTER TABLE [CRMContacts] ADD [Area] nvarchar(max) NULL CONSTRAINT DF_CRMContacts_Area DEFAULT '';

IF COL_LENGTH('CRMContacts', 'Budget') IS NULL
    ALTER TABLE [CRMContacts] ADD [Budget] nvarchar(max) NULL CONSTRAINT DF_CRMContacts_Budget DEFAULT '';

IF COL_LENGTH('Contacts', 'PropertyTypeInterests') IS NULL
    ALTER TABLE [Contacts] ADD [PropertyTypeInterests] nvarchar(max) NULL CONSTRAINT DF_Contacts_PropertyTypeInterests DEFAULT '';

IF COL_LENGTH('Contacts', 'AreaInterestIds') IS NULL
    ALTER TABLE [Contacts] ADD [AreaInterestIds] nvarchar(max) NULL CONSTRAINT DF_Contacts_AreaInterestIds DEFAULT '';

IF COL_LENGTH('AgencyRecords', 'ParentAgencyId') IS NULL
    ALTER TABLE [AgencyRecords] ADD [ParentAgencyId] nvarchar(64) NULL;

IF COL_LENGTH('AgencyRecords', 'IsTrial') IS NULL
    ALTER TABLE [AgencyRecords] ADD [IsTrial] bit NOT NULL CONSTRAINT DF_AgencyRecords_IsTrial DEFAULT 0;

IF COL_LENGTH('AgencyRecords', 'TrialStartDate') IS NULL
    ALTER TABLE [AgencyRecords] ADD [TrialStartDate] datetime2 NULL;

IF COL_LENGTH('AgencyRecords', 'TrialEndDate') IS NULL
    ALTER TABLE [AgencyRecords] ADD [TrialEndDate] datetime2 NULL;


/* ============================================================
   2. Agencies
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [AgencyRecords]
(
    [AgencyId], [TenantId], [ParentAgencyId], [Name],
    [TradingName], [RegistrationNumber], [CompanyLogoUrl],
    [ContactEmail], [HeadquartersAddress], [OperationalStatus],
    [IsTrial], [TrialStartDate], [TrialEndDate], [CreatedAt]
)
SELECT
    CONCAT('AG-', RIGHT(CONCAT('0000', N.i), 4)),
    CONCAT('TEN-', RIGHT(CONCAT('0000', N.i), 4)),
    CASE
        WHEN N.i <= 8 THEN NULL
        WHEN N.i % 50 = 0 THEN 'AG-0001'
        ELSE CONCAT('AG-', RIGHT(CONCAT('0000', (N.i % 8) + 1), 4))
    END,
    CONCAT('Agency ', N.i),
    CONCAT('Agency ', N.i),
    CONCAT('REG-', RIGHT(CONCAT('00000', N.i), 5)),
    CONCAT('https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency%20', N.i),
    CONCAT('agency', N.i, '@realestate.local'),
    CONCAT(N.i, ' Harbour Road, Cape Town, South Africa'),
    CASE
        WHEN N.i % 7 = 0 THEN 'Review'
        WHEN N.i % 11 = 0 THEN 'TrialPending'
        ELSE 'Active'
    END,
    CASE WHEN N.i % 11 = 0 THEN 1 ELSE 0 END,
    CASE WHEN N.i % 11 = 0 THEN DATEADD(DAY, -7, SYSUTCDATETIME()) ELSE NULL END,
    CASE WHEN N.i % 11 = 0 THEN DATEADD(DAY,  7, SYSUTCDATETIME()) ELSE NULL END,
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
WHERE NOT EXISTS
(
    SELECT 1
    FROM [AgencyRecords] A
    WHERE A.[AgencyId] = CONCAT('AG-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   3. Agency contacts
   ============================================================ */

INSERT INTO [Contacts]
(
    [ContactId], [AgencyId], [TenantId], [FullName], [EmailAddress],
    [PhoneNumber], [PositionJobTitle], [ContactType], [IsAgencyAdmin],
    [CreatedAt]
)
SELECT V.ContactId, V.AgencyId, V.TenantId, V.FullName, V.EmailAddress,
       V.PhoneNumber, V.PositionJobTitle, V.ContactType, V.IsAgencyAdmin,
       SYSUTCDATETIME()
FROM
(
    VALUES
    ('CON-AG-01','AG-0001','TEN-0001','Liam Nel','liam.nel@agency1.local','+27821000001','Managing Director',1,1),
    ('CON-AG-02','AG-0001','TEN-0001','Sarah du Toit','sarah.d@agency1.local','+27821000002','Leasing Broker',2,0),
    ('CON-AG-03','AG-0002','TEN-0002','Marco Botha','marco.b@agency2.local','+27821000003','Property Manager',0,0)
) V(ContactId,AgencyId,TenantId,FullName,EmailAddress,PhoneNumber,PositionJobTitle,ContactType,IsAgencyAdmin)
WHERE NOT EXISTS
(
    SELECT 1 FROM [Contacts] C WHERE C.[ContactId] = V.ContactId
);


/* ============================================================
   4. Brokers
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [BrokerRecords]
(
    [BrokerId], [TenantId], [AgencyId], [FullName], [Email],
    [Phone], [Role], [Status], [CreatedAt]
)
SELECT
    CONCAT('BRK-', RIGHT(CONCAT('0000', N.i), 4)),
    A.[TenantId],
    A.[AgencyId],
    CONCAT('Broker ', N.i),
    CONCAT('broker', N.i, '@realestate.local'),
    CONCAT('+27', RIGHT(CONCAT('000000000', 100000000 + N.i), 9)),
    CASE
        WHEN N.i % 3 = 0 THEN 'Manager'
        WHEN N.i % 2 = 0 THEN 'Senior Broker'
        ELSE 'Broker'
    END,
    'Active',
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
CROSS APPLY
(
    SELECT TOP (1) A2.[AgencyId], A2.[TenantId]
    FROM [AgencyRecords] A2
    ORDER BY
        CASE WHEN A2.[AgencyId] =
            CONCAT('AG-', RIGHT(CONCAT('0000', ((N.i - 1) % 1000) + 1), 4))
        THEN 0 ELSE 1 END,
        A2.[AgencyId]
) A
WHERE NOT EXISTS
(
    SELECT 1
    FROM [BrokerRecords] B
    WHERE B.[BrokerId] = CONCAT('BRK-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   5. Owners and tenants
   ContactType values assumed:
      0 = Owner, 1 = Tenant, 2 = other contact type
   Adjust if your enum differs.
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [Contacts]
(
    [ContactId], [TenantId], [ContactType], [FullName], [CompanyName],
    [EmailAddress], [PhoneNumber], [PrimaryLocation], [BudgetLimit],
    [Notes], [CreatedAt], [PropertyTypeInterests], [AreaInterestIds]
)
SELECT
    CONCAT('CON-OT-', RIGHT(CONCAT('0000', N.i), 4)),
    CONCAT('TEN-', RIGHT(CONCAT('0000', (N.i % 1000) + 1), 4)),
    CASE WHEN N.i % 2 = 0 THEN 0 ELSE 1 END,
    CASE WHEN N.i % 2 = 0 THEN CONCAT('Owner ', N.i) ELSE CONCAT('Tenant ', N.i) END,
    CASE WHEN N.i % 2 = 0 THEN CONCAT('Capital Holdings ', N.i) ELSE CONCAT('Tenant Group ', N.i) END,
    CASE WHEN N.i % 2 = 0 THEN CONCAT('owner', N.i, '@invest.local') ELSE CONCAT('tenant', N.i, '@occupy.local') END,
    CONCAT('+27', RIGHT(CONCAT('000000000', 200000000 + N.i), 9)),
    CASE
        WHEN N.i % 3 = 0 THEN 'Johannesburg'
        WHEN N.i % 3 = 1 THEN 'Cape Town'
        ELSE 'Durban'
    END,
    250000 + (N.i * 4000),
    CASE WHEN N.i % 2 = 0
         THEN 'Owner relationship ready for portfolio expansion.'
         ELSE 'Tenant relationship active with renewal cycle.'
    END,
    DATEADD(DAY, -N.i, SYSUTCDATETIME()),
    CASE WHEN N.i % 4 = 0 THEN 'Residential'
         WHEN N.i % 4 = 1 THEN 'Commercial'
         WHEN N.i % 4 = 2 THEN 'Retail'
         ELSE 'Industrial' END,
    CONVERT(nvarchar(max), (1 + (N.i % 6)))
FROM N
WHERE NOT EXISTS
(
    SELECT 1 FROM [Contacts] C
    WHERE C.[ContactId] = CONCAT('CON-OT-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   6. Companies
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [CRMCompanies]
(
    [CompanyId], [TenantId], [BusinessName], [RegistrationNumber],
    [VatNumber], [CorporateWebsite], [HeadOfficeAddress],
    [CorporateCreditLimit], [IndustryVertical], [CreatedAt]
)
SELECT
    CONCAT('COM-', RIGHT(CONCAT('0000', N.i), 4)),
    CONCAT('TEN-', RIGHT(CONCAT('0000', (N.i % 1000) + 1), 4)),
    CONCAT('Company ', N.i),
    CONCAT('REG-', RIGHT(CONCAT('000000', N.i), 6)),
    CONCAT('VAT-', RIGHT(CONCAT('000000', N.i), 6)),
    CONCAT('https://company', N.i, '.example.com'),
    CONCAT(N.i, ' Main Road, Johannesburg, South Africa'),
    CAST(500000 + (N.i * 5000) AS decimal(18,2)),
    CASE
        WHEN N.i % 4 = 0 THEN 'Commercial Leasing'
        WHEN N.i % 3 = 0 THEN 'Residential Sales'
        ELSE 'Property Investment'
    END,
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
WHERE NOT EXISTS
(
    SELECT 1 FROM [CRMCompanies] C
    WHERE C.[CompanyId] = CONCAT('COM-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   7. Legal entities
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [CRMLegalEntities]
(
    [LegalEntityId], [TenantId], [RegisteredLegalName],
    [AssociatedCompanyId], [EntityTypeClassifier],
    [TaxRegistrationIndex], [DeedsOfficeIdentifier], [CreatedAt]
)
SELECT
    CONCAT('LGE-', RIGHT(CONCAT('0000', N.i), 4)),
    CONCAT('TEN-', RIGHT(CONCAT('0000', (N.i % 1000) + 1), 4)),
    CONCAT('Legal Entity ', N.i),
    CONCAT('COM-', RIGHT(CONCAT('0000', ((N.i - 1) % 1000) + 1), 4)),
    CASE WHEN N.i % 2 = 0 THEN 'Special Purpose Vehicle' ELSE 'Private Company' END,
    CONCAT('TAX-', RIGHT(CONCAT('000000', N.i), 6)),
    CONCAT('DO-', RIGHT(CONCAT('000000', N.i), 6)),
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
WHERE NOT EXISTS
(
    SELECT 1 FROM [CRMLegalEntities] L
    WHERE L.[LegalEntityId] = CONCAT('LGE-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   8. General contacts
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [Contacts]
(
    [ContactId], [TenantId], [FullName], [EmailAddress], [PhoneNumber],
    [PositionJobTitle], [ContactType], [AssociatedCompanyId],
    [AssociatedLegalEntityId], [CreatedAt]
)
SELECT
    CONCAT('CON-', RIGHT(CONCAT('0000', N.i), 4)),
    CONCAT('TEN-', RIGHT(CONCAT('0000', (N.i % 1000) + 1), 4)),
    CONCAT('Contact ', N.i),
    CONCAT('contact', N.i, '@pipeline.local'),
    CONCAT('+27', RIGHT(CONCAT('000000000', 300000000 + N.i), 9)),
    CASE WHEN N.i % 2 = 0 THEN 'Director' ELSE 'Relationship Manager' END,
    N.i % 3,
    CONCAT('COM-', RIGHT(CONCAT('0000', ((N.i - 1) % 1000) + 1), 4)),
    CONCAT('LGE-', RIGHT(CONCAT('0000', ((N.i - 1) % 1000) + 1), 4)),
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
WHERE NOT EXISTS
(
    SELECT 1 FROM [Contacts] C
    WHERE C.[ContactId] = CONCAT('CON-', RIGHT(CONCAT('0000', N.i), 4))
);


/* ============================================================
   9. Leads
   Assumptions:
      LeadId is an IDENTITY integer.
      CommissionTypes.Percent is stored as 'Percent'
      CommissionTypes.Amount is stored as 'Amount'
      LeadWorkflowStage is stored as integer enum.
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [LeadRecords]
(
    [TenantId], [ClientName], [ContactEmail], [ContactPhone],
    [AllocatedBrokerIds], [LeadStage], [PropertyInterestClass],
    [PreferredArea], [EstimatedBudget], [CommissionType],
    [CommissionValue], [LeadNotes], [IsConvertedToDeal], [CreatedAt]
)
SELECT
    B.[TenantId],
    CONCAT('Lead Client ', N.i),
    CONCAT('lead', N.i, '@opportunity.local'),
    CONCAT('+27', RIGHT(CONCAT('000000000', 400000000 + N.i), 9)),
    B.[BrokerId],
    N.i % 5,
    CASE
        WHEN N.i % 3 = 0 THEN 'Residential Sale'
        WHEN N.i % 3 = 1 THEN 'Commercial Lease'
        ELSE 'Industrial Investment'
    END,
    CASE
        WHEN N.i % 4 = 0 THEN 'Cape Town'
        WHEN N.i % 4 = 1 THEN 'Johannesburg'
        WHEN N.i % 4 = 2 THEN 'Durban'
        ELSE 'Pretoria'
    END,
    CAST(150000 + (N.i * 12500) AS decimal(18,2)),
    CASE WHEN N.i % 10 < 7 THEN 'Percent' ELSE 'Amount' END,
    CASE WHEN N.i % 10 < 7 THEN CAST(3 + (N.i % 5) AS decimal(18,2))
         ELSE CAST(25000 + (N.i * 750) AS decimal(18,2)) END,
    'Prospect reviewed location, pricing and due diligence analytics.',
    CASE WHEN N.i % 6 = 0 THEN 1 ELSE 0 END,
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
CROSS APPLY
(
    SELECT TOP (1) B2.[BrokerId], B2.[TenantId]
    FROM [BrokerRecords] B2
    ORDER BY B2.[BrokerId]
    OFFSET ((N.i - 1) % NULLIF((SELECT COUNT(*) FROM [BrokerRecords]), 0)) ROWS
) B
WHERE NOT EXISTS
(
    SELECT 1
    FROM [LeadRecords] L
    WHERE L.[ClientName] = CONCAT('Lead Client ', N.i)
);


/* ============================================================
   10. Deals
   Assumptions:
      DealId is an IDENTITY integer.
      LinkedPropertyId is numeric.
      CommissionType enum is stored as text.
   ============================================================ */

;WITH N AS
(
    SELECT TOP (1000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [DealRecords]
(
    [TenantId], [AllocatedBrokerIds], [DealStage], [LinkedPropertyId],
    [BuyerClientId], [SellerClientId], [TransactionValue],
    [CommissionType], [CommissionValue], [DealNotes],
    [LastStageUpdatedAt], [CreatedAt]
)
SELECT
    B.[TenantId],
    B.[BrokerId],
    N.i % 6,
    CASE WHEN N.i % 200 = 0 THEN 200 + (N.i % 250) ELSE N.i % 900 END,
    Buyer.[ContactId],
    Seller.[ContactId],
    CAST(500000 + (N.i * 15000) AS decimal(18,2)),
    CASE WHEN N.i % 10 < 7 THEN 'Percent' ELSE 'Amount' END,
    CASE WHEN N.i % 10 < 7 THEN CAST(3 + (N.i % 5) AS decimal(18,2))
         ELSE CAST(25000 + (N.i * 750) AS decimal(18,2)) END,
    'Deal advanced through underwriting and legal review.',
    DATEADD(DAY, -N.i, SYSUTCDATETIME()),
    DATEADD(DAY, -N.i, SYSUTCDATETIME())
FROM N
CROSS APPLY
(
    SELECT TOP (1) B2.[BrokerId], B2.[TenantId]
    FROM [BrokerRecords] B2
    ORDER BY B2.[BrokerId]
    OFFSET ((N.i - 1) % NULLIF((SELECT COUNT(*) FROM [BrokerRecords]), 0)) ROWS
) B
CROSS APPLY
(
    SELECT TOP (1) C.[ContactId]
    FROM [Contacts] C
    ORDER BY C.[ContactId]
    OFFSET ((N.i - 1) % NULLIF((SELECT COUNT(*) FROM [Contacts]), 0)) ROWS
) Buyer
CROSS APPLY
(
    SELECT TOP (1) C.[ContactId]
    FROM [Contacts] C
    ORDER BY C.[ContactId]
    OFFSET ((N.i - 1) % NULLIF((SELECT COUNT(*) FROM [Contacts]), 0)) ROWS
) Seller
WHERE NOT EXISTS
(
    SELECT 1
    FROM [DealRecords] D
    WHERE D.[DealNotes] = 'Deal advanced through underwriting and legal review.'
      AND D.[CreatedAt] = DATEADD(DAY, -N.i, CONVERT(date, SYSUTCDATETIME()))
);


/* ============================================================
   11. Geography
   ============================================================ */

IF NOT EXISTS (SELECT 1 FROM [GeographyRecords])
BEGIN
    INSERT INTO [GeographyRecords]
    (
        [Country], [Province], [City], [Suburb], [Latitude], [Longitude]
    )
    VALUES
    ('South Africa','Western Cape','Cape Town','City Bowl',-33.9249,18.4207),
    ('South Africa','Western Cape','Cape Town','Rondebosch',-33.9322,18.4672),
    ('South Africa','Western Cape','Stellenbosch','Stellenbosch Central',-33.9348,18.8659),
    ('South Africa','Gauteng','Johannesburg','Sandton',-26.1027,28.0330),
    ('South Africa','Gauteng','Pretoria','Hatfield',-25.7546,28.2567),
    ('South Africa','KwaZulu-Natal','Durban','Umhlanga',-29.9558,30.9834);
END;


/* ============================================================
   12. Owner/tenant interest backfill
   ============================================================ */

;WITH OrderedContacts AS
(
    SELECT
        [ContactId],
        ROW_NUMBER() OVER (ORDER BY [ContactId]) - 1 AS rn
    FROM [Contacts]
    WHERE [ContactType] IN (0,1)
)
UPDATE C
SET
    [PropertyTypeInterests] =
        CASE O.rn % 4
            WHEN 0 THEN 'Residential'
            WHEN 1 THEN 'Commercial'
            WHEN 2 THEN 'Retail'
            ELSE 'Industrial'
        END,
    [AreaInterestIds] = CONVERT(nvarchar(max), 1 + (O.rn % 6))
FROM [Contacts] C
INNER JOIN OrderedContacts O ON O.[ContactId] = C.[ContactId]
WHERE NULLIF(LTRIM(RTRIM(C.[PropertyTypeInterests])), '') IS NULL
   OR NULLIF(LTRIM(RTRIM(C.[AreaInterestIds])), '') IS NULL;


/* ============================================================
   13. Legacy tenant distribution
   ============================================================ */

UPDATE A
SET
    [TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
        TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', ''))), 4)),
    [ParentAgencyId] =
        CASE
            WHEN TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) <= 8 THEN NULL
            WHEN TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) % 50 = 0 THEN 'AG-0001'
            ELSE CONCAT('AG-', RIGHT(CONCAT('0000',
                (TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) % 8) + 1), 4))
        END,
    [IsTrial] =
        CASE WHEN TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) % 11 = 0 THEN 1 ELSE 0 END,
    [TrialStartDate] =
        CASE WHEN TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) % 11 = 0
             THEN DATEADD(DAY, -7, SYSUTCDATETIME()) ELSE NULL END,
    [TrialEndDate] =
        CASE WHEN TRY_CONVERT(int, REPLACE([AgencyId], 'AG-', '')) % 11 = 0
             THEN DATEADD(DAY, 7, SYSUTCDATETIME()) ELSE NULL END
FROM [AgencyRecords] A
WHERE A.[TenantId] IN ('TEN-001','')
  AND A.[AgencyId] LIKE 'AG-%'
  AND TRY_CONVERT(int, REPLACE(A.[AgencyId], 'AG-', '')) IS NOT NULL;

UPDATE B
SET B.[TenantId] = A.[TenantId]
FROM [BrokerRecords] B
INNER JOIN [AgencyRecords] A ON A.[AgencyId] = B.[AgencyId]
WHERE B.[TenantId] IN ('TEN-001','');

UPDATE C
SET C.[TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
    (TRY_CONVERT(int, RIGHT(C.[CompanyId], 4)) % 1000) + 1), 4))
FROM [CRMCompanies] C
WHERE C.[TenantId] IN ('TEN-001','');

UPDATE L
SET L.[TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
    (TRY_CONVERT(int, RIGHT(L.[LegalEntityId], 4)) % 1000) + 1), 4))
FROM [CRMLegalEntities] L
WHERE L.[TenantId] IN ('TEN-001','');

UPDATE C
SET C.[TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
    (TRY_CONVERT(int, RIGHT(C.[ContactId], 4)) % 1000) + 1), 4))
FROM [Contacts] C
WHERE C.[TenantId] IN ('TEN-001','');

UPDATE L
SET L.[TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
    (ABS(L.[LeadId]) % 1000) + 1), 4))
FROM [LeadRecords] L
WHERE L.[TenantId] IN ('TEN-001','');

UPDATE D
SET D.[TenantId] = CONCAT('TEN-', RIGHT(CONCAT('0000',
    (ABS(D.[DealId]) % 1000) + 1), 4))
FROM [DealRecords] D
WHERE D.[TenantId] IN ('TEN-001','');


/* ============================================================
   14. Commission backfill
   ============================================================ */

UPDATE D
SET
    [CommissionType] = CASE WHEN D.[DealId] % 10 < 7 THEN 'Percent' ELSE 'Amount' END,
    [CommissionValue] =
        CASE WHEN D.[DealId] % 10 < 7
             THEN CAST(3 + (D.[DealId] % 5) AS decimal(18,2))
             ELSE CAST(25000 + (D.[DealId] * 750) AS decimal(18,2))
        END
FROM [DealRecords] D
WHERE ISNULL(D.[CommissionValue], 0) = 0
   OR ISNULL(D.[CommissionType], '') = '';

UPDATE L
SET
    [CommissionType] = CASE WHEN L.[LeadId] % 10 < 7 THEN 'Percent' ELSE 'Amount' END,
    [CommissionValue] =
        CASE WHEN L.[LeadId] % 10 < 7
             THEN CAST(3 + (L.[LeadId] % 5) AS decimal(18,2))
             ELSE CAST(25000 + (L.[LeadId] * 750) AS decimal(18,2))
        END
FROM [LeadRecords] L
WHERE ISNULL(L.[CommissionValue], 0) = 0
   OR ISNULL(L.[CommissionType], '') = '';


COMMIT TRANSACTION;

PRINT 'CRM seed data completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
