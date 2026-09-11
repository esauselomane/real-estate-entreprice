-- ============================================================================
-- 🏢 CRM MICROSERVICE — IDEMPOTENT DATA SEEDING
-- ============================================================================
-- Extracted from:
--   * backend-services/CRM.Microservice/Data/CRMSeedData.cs
--   * scripts.sql (property + CRM sections)
-- Run against: RealEstateCRMDB (or the CRM microservice database)
-- Notes:
--   * Uses IF NOT EXISTS so the script is re-runnable.
--   * Enum values stored as strings (LeadWorkflowStage / DealWorkflowStage /
--     CommissionTypes) to match the C# string-converted enums used in the
--     existing scripts.sql seed rows.
-- ============================================================================

USE [RealEstateCRMDB];
GO

-- ============================================================================
-- 1. AGENCY RECORDS (representative sample + hierarchy)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [AgencyRecords] WHERE [AgencyId] = N'AG-0001')
BEGIN
    INSERT INTO [AgencyRecords] (
        [AgencyId], [TenantId], [Name], [TradingName], [RegistrationNumber],
        [CompanyLogoUrl], [ContactEmail], [HeadquartersAddress], [OperationalStatus],
        [CreatedAt], [ParentAgencyId], [IsTrial], [TrialStartDate], [TrialEndDate],
        [BrokerageLicense]
    ) VALUES
    (N'AG-0001', N'TEN-0001', N'Agency 1',  N'Agency 1',  N'REG-1A2B3', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+1',  N'agency1@realestate.local',  N'1 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(DAY, -1, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0001'),
    (N'AG-0002', N'TEN-0002', N'Agency 2',  N'Agency 2',  N'REG-4C5D6', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+2',  N'agency2@realestate.local',  N'2 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(MONTH, -1, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0002'),
    (N'AG-0003', N'TEN-0003', N'Agency 3',  N'Agency 3',  N'REG-7E8F9', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+3',  N'agency3@realestate.local',  N'3 Harbour Road, Cape Town, South Africa', N'Review',   DATEADD(MONTH, -2, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0003'),
    (N'AG-0004', N'TEN-0004', N'Agency 4',  N'Agency 4',  N'REG-1G2H3', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+4',  N'agency4@realestate.local',  N'4 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(MONTH, -3, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0004'),
    (N'AG-0005', N'TEN-0005', N'Agency 5',  N'Agency 5',  N'REG-4I5J6', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+5',  N'agency5@realestate.local',  N'5 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(MONTH, -4, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0005'),
    (N'AG-0006', N'TEN-0006', N'Agency 6',  N'Agency 6',  N'REG-7K8L9', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+6',  N'agency6@realestate.local',  N'6 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(MONTH, -5, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0006'),
    (N'AG-0007', N'TEN-0007', N'Agency 7',  N'Agency 7',  N'REG-1M2N3', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+7',  N'agency7@realestate.local',  N'7 Harbour Road, Cape Town, South Africa', N'Review',   DATEADD(DAY, -5, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0007'),
    (N'AG-0008', N'TEN-0008', N'Agency 8',  N'Agency 8',  N'REG-4O5P6', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+8',  N'agency8@realestate.local',  N'8 Harbour Road, Cape Town, South Africa', N'Active',   DATEADD(MONTH, -2, GETUTCDATE()), NULL, 0, NULL, NULL, N'BL-0008'),
    (N'AG-0009', N'TEN-0009', N'Agency 9',  N'Agency 9',  N'REG-7Q8R9', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+9',  N'agency9@realestate.local',  N'9 Harbour Road, Cape Town, South Africa', N'TrialPending', DATEADD(DAY, -9, GETUTCDATE()), N'AG-0001', 1, DATEADD(DAY, -5, GETUTCDATE()), DATEADD(DAY, 7, GETUTCDATE()), N'BL-0009'),
    (N'AG-0010', N'TEN-0010', N'Agency 10', N'Agency 10', N'REG-1S2T3', N'https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=Agency+10', N'agency10@realestate.local', N'10 Harbour Road, Cape Town, South Africa', N'Active',  DATEADD(DAY, -10, GETUTCDATE()), N'AG-0001', 0, NULL, NULL, N'BL-0010');
END
GO

-- ============================================================================
-- 2. CONTACT PERSONS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [ContactPersons] WHERE [ContactPersonId] = N'CPT-00001')
BEGIN
    INSERT INTO [ContactPersons] (
        [ContactPersonId], [AgencyId], [FullName], [Email], [Phone],
        [Position], [Role], [IsAgencyAdmin], [CreatedAt]
    ) VALUES
    (N'CPT-00001', N'AG-0001', N'Liam Nel',       N'liam.nel@agency1.local',     N'+27821000001', N'Managing Director', N'Manager',  1, GETUTCDATE()),
    (N'CPT-00002', N'AG-0001', N'Sarah du Toit',  N'sarah.d@agency1.local',      N'+27821000002', N'Leasing Broker',   N'Broker',   0, GETUTCDATE()),
    (N'CPT-00003', N'AG-0002', N'Marco Botha',    N'marco.b@agency2.local',      N'+27821000003', N'Property Manager', N'Owner',    0, GETUTCDATE());
END
GO

-- ============================================================================
-- 3. BROKER RECORDS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [BrokerRecords] WHERE [BrokerId] = N'BRK-0001')
BEGIN
    INSERT INTO [BrokerRecords] (
        [BrokerId], [TenantId], [AgencyId], [FullName], [Email], [Phone],
        [Role], [Status], [CreatedAt]
    ) VALUES
    (N'BRK-0001', N'TEN-0001', N'AG-0001', N'Broker 1',  N'broker1@realestate.local',  N'+27110000001', N'Broker',       N'Active', DATEADD(DAY, -1, GETUTCDATE())),
    (N'BRK-0002', N'TEN-0002', N'AG-0002', N'Broker 2',  N'broker2@realestate.local',  N'+27110000002', N'Senior Broker', N'Active', DATEADD(MONTH, -1, GETUTCDATE())),
    (N'BRK-0003', N'TEN-0003', N'AG-0003', N'Broker 3',  N'broker3@realestate.local',  N'+27110000003', N'Manager',      N'Active', DATEADD(MONTH, -2, GETUTCDATE())),
    (N'BRK-0004', N'TEN-0004', N'AG-0004', N'Broker 4',  N'broker4@realestate.local',  N'+27110000004', N'Broker',       N'Active', DATEADD(MONTH, -3, GETUTCDATE())),
    (N'BRK-0005', N'TEN-0005', N'AG-0005', N'Broker 5',  N'broker5@realestate.local',  N'+27110000005', N'Senior Broker', N'Active', DATEADD(MONTH, -4, GETUTCDATE())),
    (N'BRK-0006', N'TEN-0006', N'AG-0006', N'Broker 6',  N'broker6@realestate.local',  N'+27110000006', N'Broker',       N'Active', DATEADD(MONTH, -5, GETUTCDATE())),
    (N'BRK-0007', N'TEN-0007', N'AG-0007', N'Broker 7',  N'broker7@realestate.local',  N'+27110000007', N'Manager',      N'Active', DATEADD(DAY, -5, GETUTCDATE())),
    (N'BRK-0008', N'TEN-0008', N'AG-0008', N'Broker 8',  N'broker8@realestate.local',  N'+27110000008', N'Broker',       N'Active', DATEADD(MONTH, -2, GETUTCDATE()));
END
GO

-- ============================================================================
-- 4. OWNER / TENANT RECORDS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [OwnerTenantRecords] WHERE [OwnerTenantId] = 1)
BEGIN
    INSERT INTO [OwnerTenantRecords] (
        [OwnerTenantId], [TenantId], [RecordType], [FullName], [CompanyName],
        [EmailAddress], [PhoneNumber], [PrimaryLocation], [BudgetLimit],
        [PropertyTypeInterests], [AreaInterestIds], [Notes], [CreatedAt]
    ) VALUES
    (1,  N'TEN-0001', N'Owner', N'Owner 1',  N'Capital Holdings 1',  N'owner1@invest.local',   N'+27200000001', N'Johannesburg', 250000.00, N'Residential',     N'1', N'Owner relationship ready for portfolio expansion.',   DATEADD(DAY, -1, GETUTCDATE())),
    (2,  N'TEN-0002', N'Tenant',N'Tenant 1', N'Tenant Group 1',      N'tenant1@occupy.local',  N'+27200000002', N'Cape Town',     250000.00, N'Commercial',      N'2', N'Tenant relationship active with renewal cycle.',       DATEADD(MONTH, -1, GETUTCDATE())),
    (3,  N'TEN-0003', N'Owner', N'Owner 2',  N'Capital Holdings 2',  N'owner2@invest.local',   N'+27200000003', N'Durban',        250000.00, N'Retail',          N'3', N'Owner relationship ready for portfolio expansion.',   DATEADD(MONTH, -2, GETUTCDATE())),
    (4,  N'TEN-0004', N'Tenant',N'Tenant 2', N'Tenant Group 2',      N'tenant2@occupy.local',  N'+27200000004', N'Johannesburg',  250000.00, N'Industrial',      N'4', N'Tenant relationship active with renewal cycle.',       DATEADD(MONTH, -3, GETUTCDATE())),
    (5,  N'TEN-0005', N'Owner', N'Owner 3',  N'Capital Holdings 3',  N'owner3@invest.local',   N'+27200000005', N'Cape Town',      250000.00, N'Residential',     N'5', N'Owner relationship ready for portfolio expansion.',   DATEADD(MONTH, -4, GETUTCDATE())),
    (6,  N'TEN-0006', N'Tenant',N'Tenant 3', N'Tenant Group 3',      N'tenant3@occupy.local',  N'+27200000006', N'Durban',        250000.00, N'Commercial',      N'6', N'Tenant relationship active with renewal cycle.',       DATEADD(MONTH, -5, GETUTCDATE()));
END
GO

-- ============================================================================
-- 5. GEOGRAPHY RECORDS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [GeographyRecords] WHERE [City] = N'Cape Town' AND [Suburb] = N'City Bowl')
BEGIN
    INSERT INTO [GeographyRecords] (
        [Country], [Province], [City], [Suburb], [Latitude], [Longitude]
    ) VALUES
    (N'South Africa', N'Western Cape',    N'Cape Town', N'City Bowl',        -33.924900, 18.420700),
    (N'South Africa', N'Western Cape',    N'Cape Town', N'Rondebosch',       -33.932200, 18.467200),
    (N'South Africa', N'Western Cape',    N'Stellenbosch', N'Stellenbosch Central', -33.934800, 18.865900),
    (N'South Africa', N'Gauteng',         N'Johannesburg', N'Sandton',      -26.102700, 28.033000),
    (N'South Africa', N'Gauteng',         N'Pretoria',     N'Hatfield',      -25.754600, 28.256700),
    (N'South Africa', N'KwaZulu-Natal',   N'Durban',       N'Umhlanga',      -29.955800, 30.983400);
END
GO

-- ============================================================================
-- 6. CRM COMPANIES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [CRMCompanies] WHERE [CompanyId] = N'COM-0001')
BEGIN
    INSERT INTO [CRMCompanies] (
        [CompanyId], [TenantId], [BusinessName], [RegistrationNumber],
        [VatNumber], [CorporateWebsite], [HeadOfficeAddress],
        [CorporateCreditLimit], [IndustryVertical], [CreatedAt]
    ) VALUES
    (N'COM-0001', N'TEN-0001', N'Company 1', N'REG-000001', N'VAT-000001', N'https://company1.example.com', N'1 Main Road, Johannesburg, South Africa', 505000.00, N'Commercial Leasing',    DATEADD(DAY, -1, GETUTCDATE())),
    (N'COM-0002', N'TEN-0002', N'Company 2', N'REG-000002', N'VAT-000002', N'https://company2.example.com', N'2 Main Road, Johannesburg, South Africa', 510000.00, N'Residential Sales',     DATEADD(MONTH, -1, GETUTCDATE())),
    (N'COM-0003', N'TEN-0003', N'Company 3', N'REG-000003', N'VAT-000003', N'https://company3.example.com', N'3 Main Road, Johannesburg, South Africa', 515000.00, N'Property Investment',   DATEADD(MONTH, -2, GETUTCDATE())),
    (N'COM-0004', N'TEN-0004', N'Company 4', N'REG-000004', N'VAT-000004', N'https://company4.example.com', N'4 Main Road, Johannesburg, South Africa', 520000.00, N'Commercial Leasing',    DATEADD(MONTH, -3, GETUTCDATE())),
    (N'COM-0005', N'TEN-0005', N'Company 5', N'REG-000005', N'VAT-000005', N'https://company5.example.com', N'5 Main Road, Johannesburg, South Africa', 525000.00, N'Residential Sales',     DATEADD(MONTH, -4, GETUTCDATE()));
END
GO

-- ============================================================================
-- 7. CRM LEGAL ENTITIES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [CRMLegalEntities] WHERE [LegalEntityId] = N'LGE-0001')
BEGIN
    INSERT INTO [CRMLegalEntities] (
        [LegalEntityId], [TenantId], [RegisteredLegalName], [AssociatedCompanyId],
        [EntityTypeClassifier], [TaxRegistrationIndex], [DeedsOfficeIdentifier], [CreatedAt]
    ) VALUES
    (N'LGE-0001', N'TEN-0001', N'Legal Entity 1', N'COM-0001', N'Private Company',         N'TAX-000001', N'DO-000001', DATEADD(DAY, -1, GETUTCDATE())),
    (N'LGE-0002', N'TEN-0002', N'Legal Entity 2', N'COM-0002', N'Special Purpose Vehicle', N'TAX-000002', N'DO-000002', DATEADD(MONTH, -1, GETUTCDATE())),
    (N'LGE-0003', N'TEN-0003', N'Legal Entity 3', N'COM-0003', N'Private Company',         N'TAX-000003', N'DO-000003', DATEADD(MONTH, -2, GETUTCDATE())),
    (N'LGE-0004', N'TEN-0004', N'Legal Entity 4', N'COM-0004', N'Special Purpose Vehicle', N'TAX-000004', N'DO-000004', DATEADD(MONTH, -3, GETUTCDATE())),
    (N'LGE-0005', N'TEN-0005', N'Legal Entity 5', N'COM-0005', N'Private Company',         N'TAX-000005', N'DO-000005', DATEADD(MONTH, -4, GETUTCDATE()));
END
GO

-- ============================================================================
-- 8. CRM CONTACTS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [CRMContacts] WHERE [ContactId] = N'CON-0001')
BEGIN
    INSERT INTO [CRMContacts] (
        [ContactId], [TenantId], [FullName], [EmailAddress], [PhoneNumber],
        [PositionJobTitle], [ContactRoleType], [AssociatedCompanyId], [AssociatedLegalEntityId], [CreatedAt]
    ) VALUES
    (N'CON-0001', N'TEN-0001', N'Contact 1', N'contact1@pipeline.local', N'+27300000001', N'Director',            N'Buyer',  N'COM-0001', N'LGE-0001', DATEADD(DAY, -1, GETUTCDATE())),
    (N'CON-0002', N'TEN-0002', N'Contact 2', N'contact2@pipeline.local', N'+27300000002', N'Relationship Manager', N'Seller', N'COM-0002', N'LGE-0002', DATEADD(MONTH, -1, GETUTCDATE())),
    (N'CON-0003', N'TEN-0003', N'Contact 3', N'contact3@pipeline.local', N'+27300000003', N'Director',            N'Broker', N'COM-0003', N'LGE-0003', DATEADD(MONTH, -2, GETUTCDATE())),
    (N'CON-0004', N'TEN-0004', N'Contact 4', N'contact4@pipeline.local', N'+27300000004', N'Relationship Manager', N'Buyer',  N'COM-0004', N'LGE-0004', DATEADD(MONTH, -3, GETUTCDATE())),
    (N'CON-0005', N'TEN-0005', N'Contact 5', N'contact5@pipeline.local', N'+27300000005', N'Director',            N'Seller', N'COM-0005', N'LGE-0005', DATEADD(MONTH, -4, GETUTCDATE())),
    (N'CON-0006', N'TEN-0006', N'Contact 6', N'contact6@pipeline.local', N'+27300000006', N'Relationship Manager', N'Broker', N'COM-0001', N'LGE-0001', DATEADD(MONTH, -5, GETUTCDATE()));
END
GO

-- ============================================================================
-- 9. LEAD RECORDS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [LeadRecords] WHERE [LeadId] = 1)
BEGIN
    SET IDENTITY_INSERT [LeadRecords] ON;
    INSERT INTO [LeadRecords] (
        [LeadId], [TenantId], [ClientName], [ContactEmail], [ContactPhone],
        [AllocatedBrokerIds], [LeadStage], [PropertyInterestClass], [PreferredArea],
        [EstimatedBudget], [LeadNotes], [IsConvertedToDeal], [CommissionType], [CommissionValue], [CreatedAt]
    ) VALUES
    (1,  N'TEN-0001', N'Lead Client 1',  N'lead1@opportunity.local',  N'+27400000001', N'BRK-0001', N'Contacted',     N'Residential Sale',      N'Cape Town',    150000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 0, N'Percent', 3.00,  DATEADD(DAY, -1, GETUTCDATE())),
    (2,  N'TEN-0002', N'Lead Client 2',  N'lead2@opportunity.local',  N'+27400000002', N'BRK-0002', N'AttemptedContact', N'Commercial Lease',  N'Johannesburg', 275000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 0, N'Percent', 4.00,  DATEADD(MONTH, -1, GETUTCDATE())),
    (3,  N'TEN-0003', N'Lead Client 3',  N'lead3@opportunity.local',  N'+27400000003', N'BRK-0003', N'Nurturing',       N'Industrial Investment', N'Durban',   400000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 0, N'Amount',  25000.00, DATEADD(MONTH, -2, GETUTCDATE())),
    (4,  N'TEN-0004', N'Lead Client 4',  N'lead4@opportunity.local',  N'+27400000004', N'BRK-0004', N'Qualified',       N'Residential Sale',      N'Pretoria',    175000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 1, N'Percent', 5.00,  DATEADD(MONTH, -3, GETUTCDATE())),
    (5,  N'TEN-0005', N'Lead Client 5',  N'lead5@opportunity.local',  N'+27400000005', N'BRK-0005', N'Disqualified',    N'Commercial Lease',      N'Cape Town',   300000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 0, N'Percent', 3.50,  DATEADD(MONTH, -4, GETUTCDATE())),
    (6,  N'TEN-0006', N'Lead Client 6',  N'lead6@opportunity.local',  N'+27400000006', N'BRK-0006', N'Contacted',       N'Industrial Investment', N'Johannesburg',500000.00, N'Prospect reviewed location, pricing and due diligence analytics.', 0, N'Amount',  50000.00, DATEADD(MONTH, -5, GETUTCDATE()));
    SET IDENTITY_INSERT [LeadRecords] OFF;
END
GO

-- ============================================================================
-- 10. DEAL RECORDS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [DealRecords] WHERE [DealId] = 1)
BEGIN
    SET IDENTITY_INSERT [DealRecords] ON;
    INSERT INTO [DealRecords] (
        [DealId], [TenantId], [AllocatedBrokerIds], [DealStage], [LinkedPropertyId],
        [BuyerClientId], [SellerClientId], [TransactionValue], [DealNotes],
        [LinkedDealIds], [LastStageUpdatedAt], [CreatedAt],
        [CommissionType], [CommissionValue], [RealisedCommission], [RealisedCommissionIsAuto]
    ) VALUES
    (1, N'TEN-0001', N'BRK-0001', N'DiscoveryNeedsAnalysis', 1, N'CON-0001', N'CON-0002', 500000.00,  N'Deal advanced through underwriting and legal review.', N'', DATEADD(DAY, -1, GETUTCDATE()), DATEADD(DAY, -1, GETUTCDATE()), N'Percent', 3.00,  NULL, 0),
    (2, N'TEN-0002', N'BRK-0002', N'Presentation',           2, N'CON-0002', N'CON-0003', 92000000.00,N'Deal advanced through underwriting and legal review.', N'', DATEADD(MONTH, -1, GETUTCDATE()), DATEADD(MONTH, -1, GETUTCDATE()), N'Percent', 4.00,  NULL, 0),
    (3, N'TEN-0003', N'BRK-0003', N'ProposalQuoteSent',      3, N'CON-0003', N'CON-0004', 1500000.00, N'Deal advanced through underwriting and legal review.', N'', DATEADD(MONTH, -2, GETUTCDATE()), DATEADD(MONTH, -2, GETUTCDATE()), N'Amount',  75000.00, NULL, 0),
    (4, N'TEN-0004', N'BRK-0004', N'Negotiation',            4, N'CON-0004', N'CON-0005', 2300000.00, N'Deal advanced through underwriting and legal review.', N'', DATEADD(MONTH, -3, GETUTCDATE()), DATEADD(MONTH, -3, GETUTCDATE()), N'Percent', 5.00,  NULL, 0),
    (5, N'TEN-0005', N'BRK-0005', N'ClosedWon',              5, N'CON-0005', N'CON-0006', 1800000.00, N'Deal advanced through underwriting and legal review.', N'', DATEADD(MONTH, -4, GETUTCDATE()), DATEADD(MONTH, -4, GETUTCDATE()), N'Percent', 6.00,  108000.00, 1),
    (6, N'TEN-0006', N'BRK-0006', N'ClosedLost',             6, N'CON-0006', N'CON-0001', 950000.00,  N'Deal advanced through underwriting and legal review.', N'', DATEADD(MONTH, -5, GETUTCDATE()), DATEADD(MONTH, -5, GETUTCDATE()), N'Amount',  25000.00, NULL, 0);
    SET IDENTITY_INSERT [DealRecords] OFF;
END
GO

-- ============================================================================
-- 11. DEAL BROKER SPLITS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [DealBrokerSplits] WHERE [DealBrokerSplitId] = 1)
BEGIN
    SET IDENTITY_INSERT [DealBrokerSplits] ON;
    INSERT INTO [DealBrokerSplits] (
        [DealBrokerSplitId], [DealId], [BrokerUserId], [SplitPercentage], [CreatedAt]
    ) VALUES
    (1, 1, N'BRK-0001', 100.00, GETUTCDATE()),
    (2, 2, N'BRK-0002', 100.00, GETUTCDATE()),
    (3, 3, N'BRK-0003', 100.00, GETUTCDATE()),
    (4, 4, N'BRK-0004', 100.00, GETUTCDATE()),
    (5, 5, N'BRK-0005', 100.00, GETUTCDATE()),
    (6, 5, N'BRK-0006', 30.00,  GETUTCDATE()),
    (7, 5, N'BRK-0007', 70.00,  GETUTCDATE());
    SET IDENTITY_INSERT [DealBrokerSplits] OFF;
END
GO

-- ============================================================================
-- 12. SUMMARY LOG (mirrors CRMSeedData.cs console output)
-- ============================================================================
SELECT 'Agencies Seeded'         = COUNT(*) FROM [AgencyRecords];
SELECT 'Contact Persons'         = COUNT(*) FROM [ContactPersons];
SELECT 'Brokers Seeded'          = COUNT(*) FROM [BrokerRecords];
SELECT 'Owners/Tenants Seeded'   = COUNT(*) FROM [OwnerTenantRecords];
SELECT 'Companies Seeded'        = COUNT(*) FROM [CRMCompanies];
SELECT 'Legal Entities Mapped'   = COUNT(*) FROM [CRMLegalEntities];
SELECT 'CRM Contacts Registered' = COUNT(*) FROM [CRMContacts];
SELECT 'Lead Records Seeded'     = COUNT(*) FROM [LeadRecords];
SELECT 'Deal Records Seeded'     = COUNT(*) FROM [DealRecords];
SELECT 'Geography Seeded'        = COUNT(*) FROM [GeographyRecords];
SELECT 'Broker Splits Mapped'    = COUNT(*) FROM [DealBrokerSplits];
GO
