-- ============================================================================
-- 🏠 PHASE 1: POPULATE DEEP REAL ESTATE MASTER ASSETS & UNIT SUB-LEDGERS
-- ============================================================================
-- ============================================================================
-- 🏠 ARCHITECTURE PLATFORM DDL: PROPERTY CATALOG SCHEMA DESIGN 
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'RealEstatePropertyDB')
BEGIN
    CREATE DATABASE [RealEstatePropertyDB];
END
GO

USE [RealEstatePropertyDB];
GO

-- 1. Create Lookup Structure for Normalized Property Types
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PropertyTypes]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PropertyTypes] (
        [PropertyTypeId] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(50) NOT NULL,
        CONSTRAINT [PK_PropertyTypes] PRIMARY KEY CLUSTERED ([PropertyTypeId] ASC)
    );
END
GO

-- 2. Create Expanded Master Property Assets Catalog Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PropertyAssets]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PropertyAssets] (
        [PropertyId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [CompleteAddress] NVARCHAR(MAX) NOT NULL,
        [ErfSpatialSize] NVARCHAR(50) NOT NULL,
        [TitleDeedType] NVARCHAR(100) NOT NULL,
        [PortfolioZoning] NVARCHAR(100) NOT NULL,
        [MarketClass] NVARCHAR(20) NOT NULL DEFAULT 'Sale', -- Sale vs Rental fork
        [PropertyTypeId] INT NOT NULL,
        [ValuationPrice] DECIMAL(18,2) NOT NULL,
        [SalePrice] DECIMAL(18,2) NULL,                      -- Populated if MarketClass == 'Sale'
        [BaseRentalRate] DECIMAL(18,2) NULL,                  -- Populated if MarketClass == 'Rental'
        [RentEscalationPercentage] DECIMAL(5,2) NULL,         -- Compound annual percentage rate
        [LeaseTermMonths] INT NULL,                           -- Term duration allocation
        [TenantInstallationAllowance] DECIMAL(18,2) NOT NULL DEFAULT 0.00, -- Numerical incentive value
        [Latitude] DECIMAL(9,6) NOT NULL,
        [Longitude] DECIMAL(9,6) NOT NULL,
        [VerificationDocumentsJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [LinkedOwnerId] NVARCHAR(50) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_PropertyAssets] PRIMARY KEY CLUSTERED ([PropertyId] ASC),
        CONSTRAINT [FK_PropertyAssets_PropertyTypes] FOREIGN KEY ([PropertyTypeId]) 
            REFERENCES [dbo].[PropertyTypes] ([PropertyTypeId])
    );
END
GO

-- 3. Create Granular Multi-Tenant Sub-Unit Ledger Pockets Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PropertyUnits]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PropertyUnits] (
        [PropertyUnitId] INT IDENTITY(1,1) NOT NULL,
        [PropertyId] INT NOT NULL,
        [UnitNumber] NVARCHAR(20) NOT NULL,
        [UnitDescription] NVARCHAR(100) NOT NULL,
        [NetRentableAreaSqM] DECIMAL(10,2) NOT NULL,
        [SpecificUnitRentalPrice] DECIMAL(18,2) NULL,
        [SpecificUnitSalePrice] DECIMAL(18,2) NULL,
        [UnitImagesJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [IsCurrentlyVacant] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_PropertyUnits] PRIMARY KEY CLUSTERED ([PropertyUnitId] ASC),
        CONSTRAINT [FK_PropertyUnits_PropertyAssets] FOREIGN KEY ([PropertyId]) 
            REFERENCES [dbo].[PropertyAssets] ([PropertyId]) ON DELETE CASCADE
    );
END
GO

-- 4. Create Kafka Syndication External Platforms Lookup Directory Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ExternalPlatforms]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ExternalPlatforms] (
        [PlatformId] INT IDENTITY(1,1) NOT NULL,
        [PlatformName] NVARCHAR(100) NOT NULL,
        [BaseUrlAddress] NVARCHAR(MAX) NOT NULL,
        CONSTRAINT [PK_ExternalPlatforms] PRIMARY KEY CLUSTERED ([PlatformId] ASC)
    );
END
GO

-- 5. Create Multi-Tenant External Portal Syndication Activity Mapping Junction Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PropertyPlatformSyndications]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PropertyPlatformSyndications] (
        [SyndicationId] INT IDENTITY(1,1) NOT NULL,
        [PropertyId] INT NOT NULL,
        [PlatformId] INT NOT NULL,
        [IsSyndicatedActive] BIT NOT NULL DEFAULT 0,
        [ExternalReferenceIndex] NVARCHAR(50) NOT NULL DEFAULT '',
        [LivePortalListingUrl] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [LastSynchronizedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_PropertyPlatformSyndications] PRIMARY KEY CLUSTERED ([SyndicationId] ASC),
        CONSTRAINT [FK_Syndication_PropertyAssets] FOREIGN KEY ([PropertyId]) REFERENCES [dbo].[PropertyAssets] ([PropertyId]),
        CONSTRAINT [FK_Syndication_ExternalPlatforms] FOREIGN KEY ([PlatformId]) REFERENCES [dbo].[ExternalPlatforms] ([PlatformId])
    );
END
GO

USE [RealEstatePropertyDB];
GO

-- A. Clear Existing Downstream Tables Safely to Guarantee Clean Re-runs
DELETE FROM [PropertyUnits];
DELETE FROM [PropertyPlatformSyndications];
DELETE FROM [PropertyAssets];
DELETE FROM [PropertyTypes];
GO

-- B. Populate Normalized Lookup Directory Values
SET IDENTITY_INSERT [PropertyTypes] ON;
INSERT INTO [PropertyTypes] ([PropertyTypeId], [Name]) VALUES
(1, 'Industrial'),
(2, 'Retail'),
(3, 'Commercial'),
(4, 'Residential');
SET IDENTITY_INSERT [PropertyTypes] OFF;
GO

-- C. Populate Deep Production-Grade Master Property Inventory Records
SET IDENTITY_INSERT [PropertyAssets] ON;
INSERT INTO [PropertyAssets] (
    [PropertyId], [TenantId], [CompleteAddress], [ErfSpatialSize], 
    [TitleDeedType], [PortfolioZoning], [MarketClass], [PropertyTypeId], 
    [ValuationPrice], [SalePrice], [BaseRentalRate], [RentEscalationPercentage], 
    [LeaseTermMonths], [TenantInstallationAllowance], [Latitude], [Longitude], 
    [VerificationDocumentsJson], [LinkedOwnerId], [CreatedAt]
) VALUES
(1, 'TEN-001', '42 Beach Road, Green Point, Cape Town, 8005, South Africa', '1,450 m²', 
 'Freehold', 'Commercial Sector 2', 'Rental', 3, 
 45000000.00, NULL, 185000.00, 8.25, 60, 450000.00, -33.905200, 18.402400, 
 '["deed_corp_beach42.pdf", "compliance_fire_2026.pdf"]', 'O-8801', GETUTCDATE()),

(2, 'TEN-001', 'Building B, 156 Rivonia Road, Sandton, Johannesburg, 2196, South Africa', '4,200 m²', 
 'Freehold', 'Industrial Heavy G1', 'Rental', 1, 
 92000000.00, NULL, 340000.00, 7.50, 36, 1200000.00, -26.101500, 28.056700, 
 '["zoning_permit_heavy.pdf"]', 'O-4402', GETUTCDATE()),

(3, 'TEN-001', 'Penthouse 902, 12 Ocean View Drive, Umhlanga, Durban, 4319, South Africa', '380 m²', 
 'Sectional Title', 'Residential Premium', 'Sale', 4, 
 14500000.00, 13950000.00, NULL, NULL, NULL, 0.00, -29.725400, 31.087200, 
 '["sectional_plan_u902.pdf"]', 'O-1109', GETUTCDATE());
SET IDENTITY_INSERT [PropertyAssets] OFF;
GO
-- ============================================================================
-- 🏢 PHASE 2: INJECT GRANULAR MULTI-TENANT SUB-UNIT LEDGER ROWS
-- ============================================================================

SET IDENTITY_INSERT [PropertyUnits] ON;
INSERT INTO [PropertyUnits] (
    [PropertyUnitId], [PropertyId], [UnitNumber], [UnitDescription], 
    [NetRentableAreaSqM], [SpecificUnitRentalPrice], [SpecificUnitSalePrice], 
    [UnitImagesJson], [IsCurrentlyVacant], [CreatedAt]
) VALUES
-- Multi-Tenant Pockets for Property 1 (Cape Town Commercial Office Tower)
(1, 1, 'Suite 101-A', 'Ground floor prime corporate showroom space with premium glass curtain facade', 
 450.00, 65000.00, NULL, '["primary_facade_hero.jpg", "reception_entrance_lobby.jpg"]', 1, GETUTCDATE()),

(2, 1, 'Suite 202-B', 'First floor open-plan execution hub equipped with central HVAC and executive boardroom pockets', 
 600.00, 85000.00, NULL, '["interior_boardroom_hub.jpg"]', 1, GETUTCDATE()),

(3, 1, 'Suite 303-C', 'Penthouse tier dynamic startup tech incubator workspace carrying exclusive mountain view balcony terraces', 
 400.00, 35000.00, NULL, '["aerial_transit_siting.jpg"]', 1, GETUTCDATE()),

-- Multi-Tenant Pockets for Property 2 (Sandton Heavy Logistics & Industrial Hub)
(4, 2, 'Bay 1-Alpha', 'Double-volume fulfillment center warehouse floor featuring 9m internal eave heights and 3 automated dock levelers', 
 2500.00, 210000.00, NULL, '["warehouse_clear_span.jpg", "dock_dispatch_bays.jpg"]', 1, GETUTCDATE()),

(5, 2, 'Bay 2-Bravo', 'Adjoining manufacturing bay configured with a high-load floor grid plate and 150-amp three-phase power supply taps', 
 1700.00, 130000.00, NULL, '["warehouse_clear_span.jpg"]', 1, GETUTCDATE());
SET IDENTITY_INSERT [PropertyUnits] OFF;
GO

-- Verify spatial insertion metrics in logging screens
SELECT 'Property Assets Seeded' = COUNT(*), 'Market Class Lease' = SUM(CASE WHEN [MarketClass]='Rental' THEN 1 ELSE 0 END) FROM [PropertyAssets];
SELECT 'Granular Sub-Units Mapped' = COUNT(*), 'Total NRA Available (m²)' = SUM([NetRentableAreaSqM]) FROM [PropertyUnits];
GO
-- ============================================================================
-- 🤝 PHASE 3: POPULATE ENTERPRISE CRM COMPANIES, ENTITIES, AND CONTACTS
-- ============================================================================

-- ============================================================================
-- 🤝 ARCHITECTURE PLATFORM DDL: CUSTOMER RELATIONSHIP MANAGEMENT SCHEMA DESIGN
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'RealEstateCRMDB')
BEGIN
    CREATE DATABASE [RealEstateCRMDB];
END
GO

USE [RealEstateCRMDB];
GO

-- 1. Create Parent Commercial Corporate Company Registry Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CRMCompanies]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CRMCompanies] (
        [CompanyId] NVARCHAR(50) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [BusinessName] NVARCHAR(255) NOT NULL,
        [RegistrationNumber] NVARCHAR(100) NOT NULL DEFAULT '',
        [VatNumber] NVARCHAR(100) NOT NULL DEFAULT '',
        [CorporateWebsite] NVARCHAR(255) NOT NULL DEFAULT '',
        [HeadOfficeAddress] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [CorporateCreditLimit] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [IndustryVertical] NVARCHAR(100) NOT NULL DEFAULT 'Real Estate Investment',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CRMCompanies] PRIMARY KEY CLUSTERED ([CompanyId] ASC)
    );
END
GO

-- 2. Create Contractual Lease Signing Legal Entities Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CRMLegalEntities]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CRMLegalEntities] (
        [LegalEntityId] NVARCHAR(50) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [RegisteredLegalName] NVARCHAR(255) NOT NULL,
        [AssociatedCompanyId] NVARCHAR(50) NULL,
        [EntityTypeClassifier] NVARCHAR(100) NOT NULL DEFAULT 'Private Company',
        [TaxRegistrationIndex] NVARCHAR(100) NOT NULL DEFAULT '',
        [DeedsOfficeIdentifier] NVARCHAR(100) NOT NULL DEFAULT '',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CRMLegalEntities] PRIMARY KEY CLUSTERED ([LegalEntityId] ASC),
        CONSTRAINT [FK_CRMLegalEntities_CRMCompanies] FOREIGN KEY ([AssociatedCompanyId]) 
            REFERENCES [dbo].[CRMCompanies] ([CompanyId])
    );
END
GO

-- 3. Create Comprehensive Human Contacts Profile Directory Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CRMContacts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CRMContacts] (
        [ContactId] NVARCHAR(50) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [FullName] NVARCHAR(255) NOT NULL,
        [EmailAddress] NVARCHAR(255) NOT NULL DEFAULT '',
        [PhoneNumber] NVARCHAR(50) NOT NULL DEFAULT '',
        [PositionJobTitle] NVARCHAR(100) NOT NULL DEFAULT 'Director',
        [ContactRoleType] NVARCHAR(50) NOT NULL DEFAULT 'Buyer', -- Buyer, Seller, Tenant
        [AssociatedCompanyId] NVARCHAR(50) NULL,
        [AssociatedLegalEntityId] NVARCHAR(50) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CRMContacts] PRIMARY KEY CLUSTERED ([ContactId] ASC),
        CONSTRAINT [FK_CRMContacts_CRMCompanies] FOREIGN KEY ([AssociatedCompanyId]) 
            REFERENCES [dbo].[CRMCompanies] ([CompanyId]),
        CONSTRAINT [FK_CRMContacts_CRMLegalEntities] FOREIGN KEY ([AssociatedLegalEntityId]) 
            REFERENCES [dbo].[CRMLegalEntities] ([LegalEntityId])
    );
END
GO

-- 4. Create Pre-Verification Lead Capture Registry Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LeadRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LeadRecords] (
        [LeadId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [ClientName] NVARCHAR(255) NOT NULL,
        [ContactEmail] NVARCHAR(255) NOT NULL DEFAULT '',
        [ContactPhone] NVARCHAR(50) NOT NULL DEFAULT '',
        [AllocatedToAgent] NVARCHAR(255) NOT NULL,
        [LeadStage] NVARCHAR(50) NOT NULL DEFAULT 'Contacted', -- Enum mapped as string labels
        [PropertyInterestClass] NVARCHAR(100) NOT NULL,
        [PreferredArea] NVARCHAR(255) NOT NULL,
        [EstimatedBudget] DECIMAL(18,2) NOT NULL,
        [LeadNotes] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [IsConvertedToDeal] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_LeadRecords] PRIMARY KEY CLUSTERED ([LeadId] ASC)
    );
END
GO

-- 5. Create Post-Verification Active Enterprise Deals Board Pipeline Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DealRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DealRecords] (
        [DealId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] NVARCHAR(50) NOT NULL,
        [AllocatedToAgent] NVARCHAR(255) NOT NULL,
        [DealStage] NVARCHAR(50) NOT NULL DEFAULT 'DiscoveryNeedsAnalysis', -- Enum mapped as string labels
        [LinkedPropertyId] INT NULL,
        [BuyerClientId] NVARCHAR(50) NOT NULL,
        [SellerClientId] NVARCHAR(50) NOT NULL,
        [TransactionValue] DECIMAL(18,2) NOT NULL,
        [DealNotes] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [LastStageUpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_DealRecords] PRIMARY KEY CLUSTERED ([DealId] ASC)
    );
END
GO

USE [RealEstateCRMDB];
GO

-- A. Clear Existing Tables Safely to Maintain Referential Integrity
DELETE FROM [CRMContacts];
DELETE FROM [CRMLegalEntities];
DELETE FROM [CRMCompanies];
DELETE FROM [LeadRecords];
DELETE FROM [DealRecords];
GO

-- B. Seed Parent Commercial Companies (Accounts Ledger)
INSERT INTO [CRMCompanies] (
    [CompanyId], [TenantId], [BusinessName], [RegistrationNumber], 
    [VatNumber], [CorporateWebsite], [HeadOfficeAddress], 
    [CorporateCreditLimit], [IndustryVertical], [CreatedAt]
) VALUES
('COM-APEX', 'TEN-001', 'Apex Corporate Property Group', '2018/489021/07', 
 '4019920194', 'https://apexpropertygroup.co.za', '11 West Street, Sandown, Sandton, 2196', 
 50000000.00, 'Real Estate Development & Investment', GETUTCDATE()),

('COM-BLU', 'TEN-001', 'Blue Horizon Logistics Ltd', '2021/110482/06', 
 '4992018471', 'https://bluehorizonlogistics.com', 'Unit 4, Airport Industrial Park, Cape Town, 7490', 
 25000000.00, 'Supply Chain & Heavy Logistics', GETUTCDATE());
GO

-- C. Seed Contractual Legal Entities (Signing Authorities)
INSERT INTO [CRMLegalEntities] (
    [LegalEntityId], [TenantId], [RegisteredLegalName], [AssociatedCompanyId], 
    [EntityTypeClassifier], [TaxRegistrationIndex], [DeedsOfficeIdentifier], [CreatedAt]
) VALUES
('LGE-APX-PTY', 'TEN-001', 'Apex Corporate Properties (Pty) Ltd', 'COM-APEX', 
 'Private Company', '9019284711', 'CAPE-DEEDS-77102/B', GETUTCDATE()),

('LGE-BLU-PROP', 'TEN-001', 'Blue Horizon Property Holdings Trust', 'COM-BLU', 
 'Trust Structure', '9440182744', 'JHB-DEEDS-99201/A', GETUTCDATE());
GO

-- D. Seed Human Contacts Directory (Brokers, Stakeholders, Buyers, Sellers)
INSERT INTO [CRMContacts] (
    [ContactId], [TenantId], [FullName], [EmailAddress], [PhoneNumber], 
    [PositionJobTitle], [ContactRoleType], [AssociatedCompanyId], [AssociatedLegalEntityId], [CreatedAt]
) VALUES
-- Internal Registered Platform Brokers
('CON-BROK01', 'TEN-001', 'Sarah Jenkins', 's.jenkins@apexluxury.co.za', '+27118845000', 
 'Principal Commercial Broker', 'General', 'COM-APEX', 'LGE-APX-PTY', GETUTCDATE()),

-- Corporate Clients / Prospects
('CON-CUST01', 'TEN-001', 'Arthur Pendelton', 'a.pendelton@bluehorizon.com', '+27219341102', 
 'Chief Procurement Officer', 'Buyer', 'COM-BLU', 'LGE-BLU-PROP', GETUTCDATE()),

('CON-CUST02', 'TEN-001', 'Thabo Mokoena', 'thabo@mokoenainvestments.co.za', '+27834419021', 
 'Managing Director', 'Buyer', NULL, NULL, GETUTCDATE());
GO

-- E. Seed Live CRM Sales Workflow Deal Trackers (Presentation Phase Hooks)
INSERT INTO [DealRecords] (
    [TenantId], [AllocatedToAgent], [DealStage], [LinkedPropertyId], 
    [BuyerClientId], [SellerClientId], [TransactionValue], [DealNotes], 
    [LastStageUpdatedAt], [CreatedAt]
) VALUES
('TEN-001', 'Sarah Jenkins', 'Presentation', 2, 
 'CON-CUST01', 'CON-BROK01', 92000000.00, '[Presentation Cycle Active]: Client expressing intent on renting Sandton Heavy Industrial Bay 1-Alpha for a 36-month timeline. Brochure payload compiler initialized.', 
 GETUTCDATE(), GETUTCDATE());
GO

-- Display summary log of seeded values
SELECT 'Companies Seeded' = COUNT(*) FROM [CRMCompanies];
SELECT 'Legal Entities Mapped' = COUNT(*) FROM [CRMLegalEntities];
SELECT 'Human Contacts Registered' = COUNT(*) FROM [CRMContacts];
SELECT 'Active Live Deals Seeding Profile' = COUNT(*) FROM [DealRecords] WHERE [DealStage] = 'Presentation';
GO
