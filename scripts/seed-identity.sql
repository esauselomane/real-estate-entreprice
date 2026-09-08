-- ============================================================================
-- 🪪 IDENTITY MICROSERVICE — IDEMPOTENT DATA SEEDING
-- ============================================================================
-- Extracted from: backend-services/Identity.Microservice/Data/IdentitySeedData.cs
-- Run against: RealEstateIdentityDB (or the Identity microservice database)
-- Notes:
--   * Uses MERGE/IF NOT EXISTS patterns so the script is re-runnable.
--   * Enum values are stored as integers (EF Core default) or strings depending
--     on your configured value converter. Adjust if your mapping differs.
-- ============================================================================

USE [RealEstateIdentityDB];
GO

-- ============================================================================
-- 1. TENANT AGENCIES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [TenantAgencies] WHERE [TenantId] = N'TEN-001')
BEGIN
    INSERT INTO [TenantAgencies] (
        [TenantId], [AgencyName], [LogoInitials], [ContactEmail],
        [HeadquartersAddress], [OperationalStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate], [ParentTenantId]
    ) VALUES
    (N'TEN-001', N'Agency 1', N'A1', N'ops1@agency1.local',
     N'1 Commercial Avenue, Cape Town, South Africa', N'TrialPending', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()), NULL);
END
GO

IF NOT EXISTS (SELECT 1 FROM [TenantAgencies] WHERE [TenantId] = N'TEN-002')
BEGIN
    INSERT INTO [TenantAgencies] (
        [TenantId], [AgencyName], [LogoInitials], [ContactEmail],
        [HeadquartersAddress], [OperationalStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate], [ParentTenantId]
    ) VALUES
    (N'TEN-002', N'Agency 2', N'A2', N'ops2@agency2.local',
     N'2 Commercial Avenue, Cape Town, South Africa', N'Active', GETUTCDATE(),
     0, NULL, NULL, NULL);
END
GO

-- ============================================================================
-- 2. USER ACCOUNTS (canonical role accounts + sample users)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-ADMIN')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-ADMIN', N'TEN-001', N'Agency Administrator', N'admin@agency.local',
     N'Password123!', N'SuperAdmin', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-ADMIN-PLAT')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-ADMIN-PLAT', N'TEN-001', N'Platform Administrator', N'platform.admin@agency.local',
     N'Password123!', N'PlatformAdmin', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-ADMIN-AGY')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-ADMIN-AGY', N'TEN-001', N'Agency Administrator', N'agency.admin@agency.local',
     N'Password123!', N'AgencyAdmin', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-BROKER-01')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-BROKER-01', N'TEN-001', N'Sales Broker', N'broker@agency.local',
     N'Password123!', N'Broker', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-OWNER-01')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-OWNER-01', N'TEN-001', N'Owner', N'owner@agency.local',
     N'Password123!', N'Owner', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccounts] WHERE [UserId] = N'USR-TENANT-01')
BEGIN
    INSERT INTO [UserAccounts] (
        [UserId], [TenantId], [FullName], [Email], [PasswordHash],
        [SystemRole], [AvatarUrl], [AccountStatus], [CreatedAt],
        [IsTrial], [TrialStartDate], [TrialEndDate]
    ) VALUES
    (N'USR-TENANT-01', N'TEN-001', N'Tenant', N'tenant@agency.local',
     N'Password123!', N'Tenant', N'', N'Active', GETUTCDATE(),
     1, GETUTCDATE(), DATEADD(DAY, 14, GETUTCDATE()));
END
GO

-- ============================================================================
-- 3. USER ACCESS CLAIMS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [UserAccessClaims] WHERE [UserId] = N'USR-ADMIN')
BEGIN
    INSERT INTO [UserAccessClaims] (
        [UserId], [CanDeleteProperties], [CanExportFinancialReports], [CanBypassValuationStages]
    ) VALUES
    (N'USR-ADMIN', 1, 1, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [UserAccessClaims] WHERE [UserId] = N'USR-BROKER-01')
BEGIN
    INSERT INTO [UserAccessClaims] (
        [UserId], [CanDeleteProperties], [CanExportFinancialReports], [CanBypassValuationStages]
    ) VALUES
    (N'USR-BROKER-01', 0, 1, 0);
END
GO

-- ============================================================================
-- 4. SYSTEM ROLES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [SystemRoles] WHERE [Name] = N'PlatformAdmin')
BEGIN
    INSERT INTO [SystemRoles] (
        [Name], [Description], [IsPlatformScoped], [IsAgencyScoped], [PermissionsJson], [IsActive], [CreatedAt]
    ) VALUES
    (N'PlatformAdmin', N'Platform-wide administrator with full provisioning authority.', 1, 0, N'[]', 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemRoles] WHERE [Name] = N'AgencyAdmin')
BEGIN
    INSERT INTO [SystemRoles] (
        [Name], [Description], [IsPlatformScoped], [IsAgencyScoped], [PermissionsJson], [IsActive], [CreatedAt]
    ) VALUES
    (N'AgencyAdmin', N'Agency-level administrator managing brokers and owners.', 0, 1, N'[]', 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemRoles] WHERE [Name] = N'Broker')
BEGIN
    INSERT INTO [SystemRoles] (
        [Name], [Description], [IsPlatformScoped], [IsAgencyScoped], [PermissionsJson], [IsActive], [CreatedAt]
    ) VALUES
    (N'Broker', N'Sales broker working a pipeline of deals.', 0, 1, N'[]', 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemRoles] WHERE [Name] = N'Owner')
BEGIN
    INSERT INTO [SystemRoles] (
        [Name], [Description], [IsPlatformScoped], [IsAgencyScoped], [PermissionsJson], [IsActive], [CreatedAt]
    ) VALUES
    (N'Owner', N'Property owner surfacing leads and interest.', 0, 0, N'[]', 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemRoles] WHERE [Name] = N'Tenant')
BEGIN
    INSERT INTO [SystemRoles] (
        [Name], [Description], [IsPlatformScoped], [IsAgencyScoped], [PermissionsJson], [IsActive], [CreatedAt]
    ) VALUES
    (N'Tenant', N'Rental tenant with maintenance request access.', 0, 0, N'[]', 1, GETUTCDATE());
END
GO

-- ============================================================================
-- 5. PERMISSIONS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Key] = N'properties:create')
BEGIN
    INSERT INTO [Permissions] ([Key], [Module], [Action], [Description], [IsActive], [CreatedAt]) VALUES
    (N'properties:create', N'Properties', N'create', N'Can add property', 1, GETUTCDATE()),
    (N'properties:view',   N'Properties', N'view',   N'Can view properties', 1, GETUTCDATE()),
    (N'properties:edit',   N'Properties', N'edit',   N'Can edit property', 1, GETUTCDATE()),
    (N'properties:delete', N'Properties', N'delete', N'Can delete properties', 1, GETUTCDATE()),
    (N'units:create',      N'Units',      N'create', N'Can add units', 1, GETUTCDATE()),
    (N'units:view',        N'Units',      N'view',   N'Can view units', 1, GETUTCDATE()),
    (N'units:edit',        N'Units',      N'edit',   N'Can edit units', 1, GETUTCDATE()),
    (N'units:delete',      N'Units',      N'delete', N'Can delete units', 1, GETUTCDATE()),
    (N'agencies:create',   N'Agencies',   N'create', N'Can create agency', 1, GETUTCDATE()),
    (N'agencies:view',     N'Agencies',   N'view',   N'Can view agencies', 1, GETUTCDATE()),
    (N'agencies:edit',     N'Agencies',   N'edit',   N'Can edit agency', 1, GETUTCDATE()),
    (N'agencies:delete',   N'Agencies',   N'delete', N'Can delete agency', 1, GETUTCDATE()),
    (N'users:create',      N'Users',      N'create', N'Can create user', 1, GETUTCDATE()),
    (N'users:view',        N'Users',      N'view',   N'Can view users', 1, GETUTCDATE()),
    (N'users:edit',        N'Users',      N'edit',   N'Can edit user', 1, GETUTCDATE()),
    (N'users:delete',      N'Users',      N'delete', N'Can delete user', 1, GETUTCDATE()),
    (N'system:roles:manage',    N'System', N'manage', N'Can manage system roles', 1, GETUTCDATE()),
    (N'system:permissions:manage', N'System', N'manage', N'Can manage permissions', 1, GETUTCDATE()),
    (N'billing:view',      N'Billing',    N'view',   N'Can view billing', 1, GETUTCDATE()),
    (N'billing:export',    N'Billing',    N'export', N'Can export financial reports', 1, GETUTCDATE()),
    (N'valuations:bypass', N'Valuations', N'bypass', N'Can bypass valuation stages', 1, GETUTCDATE());
END
GO

-- ============================================================================
-- 6. ROLE PERMISSIONS (junction)
-- ============================================================================
DECLARE @PlatformAdminRoleId INT = (SELECT [RoleDefId] FROM [SystemRoles] WHERE [Name] = N'PlatformAdmin');
DECLARE @AgencyAdminRoleId  INT = (SELECT [RoleDefId] FROM [SystemRoles] WHERE [Name] = N'AgencyAdmin');
DECLARE @BrokerRoleId       INT = (SELECT [RoleDefId] FROM [SystemRoles] WHERE [Name] = N'Broker');

-- PlatformAdmin gets all permissions
IF @PlatformAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermissions] ([RoleDefId], [PermissionId])
    SELECT @PlatformAdminRoleId, [PermissionId] FROM [Permissions]
    WHERE NOT EXISTS (
        SELECT 1 FROM [RolePermissions] WHERE [RoleDefId] = @PlatformAdminRoleId AND [PermissionId] = [Permissions].[PermissionId]
    );
END
GO

-- AgencyAdmin gets agency, user, billing, and valuation permissions
IF @AgencyAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermissions] ([RoleDefId], [PermissionId])
    SELECT @AgencyAdminRoleId, [PermissionId] FROM [Permissions]
    WHERE [Key] IN (
        N'agencies:create', N'agencies:view', N'agencies:edit', N'agencies:delete',
        N'users:create', N'users:view', N'users:edit', N'users:delete',
        N'properties:view', N'units:view', N'billing:view', N'billing:export', N'valuations:bypass'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [RolePermissions] WHERE [RoleDefId] = @AgencyAdminRoleId AND [PermissionId] = [Permissions].[PermissionId]
    );
END
GO

-- Broker gets view permissions plus property/unit create/edit
IF @BrokerRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermissions] ([RoleDefId], [PermissionId])
    SELECT @BrokerRoleId, [PermissionId] FROM [Permissions]
    WHERE [Key] IN (
        N'properties:view', N'properties:create', N'properties:edit',
        N'units:view', N'units:create', N'units:edit',
        N'agencies:view', N'users:view', N'billing:view'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [RolePermissions] WHERE [RoleDefId] = @BrokerRoleId AND [PermissionId] = [Permissions].[PermissionId]
    );
END
GO

-- ============================================================================
-- 7. PRICING PACKAGES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [PricingPackages] WHERE [Code] = N'SME_AGENCY')
BEGIN
    INSERT INTO [PricingPackages] (
        [Code], [Name], [Description], [MonthlyPrice], [AnnualPrice],
        [FeaturesJson], [SortOrder], [IsActive], [CreatedAt]
    ) VALUES
    (N'SME_AGENCY', N'SME Agency',
     N'Ideal for small agencies getting started with digital deal management.',
     299.00, 2990.00,
     N'["Up to 5 users","20 property listings","Basic brochure generation","Email support","Standard CRM pipeline"]',
     1, 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [PricingPackages] WHERE [Code] = N'PRIVATE')
BEGIN
    INSERT INTO [PricingPackages] (
        [Code], [Name], [Description], [MonthlyPrice], [AnnualPrice],
        [FeaturesJson], [SortOrder], [IsActive], [CreatedAt]
    ) VALUES
    (N'PRIVATE', N'Private',
     N'For growing agencies that need more power and reach.',
     799.00, 7990.00,
     N'["Up to 20 users","Unlimited listings","Advanced brochure + image processing","Priority support","Market syndication","Custom branding"]',
     2, 1, GETUTCDATE());
END
GO

IF NOT EXISTS (SELECT 1 FROM [PricingPackages] WHERE [Code] = N'ENTERPRISE')
BEGIN
    INSERT INTO [PricingPackages] (
        [Code], [Name], [Description], [MonthlyPrice], [AnnualPrice],
        [FeaturesJson], [SortOrder], [IsActive], [CreatedAt]
    ) VALUES
    (N'ENTERPRISE', N'Enterprise',
     N'Full-scale platform access for large organizations with custom needs.',
     1999.00, 19990.00,
     N'["Unlimited users","Unlimited listings","Dedicated account manager","API access","Custom integrations","On-premise option","SLA guarantee","Advanced analytics"]',
     3, 1, GETUTCDATE());
END
GO

-- ============================================================================
-- 8. LEAD CAPTURE REQUESTS (sample public intake records)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [LeadCaptureRequests] WHERE [Email] = N'lead.sample1@opportunity.local')
BEGIN
    INSERT INTO [LeadCaptureRequests] (
        [FullName], [Email], [Phone], [Company], [UserType], [Package],
        [DemoRequested], [Message], [Source], [Status], [CreatedAt]
    ) VALUES
    (N'Lead Sample 1', N'lead.sample1@opportunity.local', N'+27820000001', N'Sample Co', 0, 1, 1, N'Interested in SME Agency', N'website', 0, GETUTCDATE()),
    (N'Lead Sample 2', N'lead.sample2@opportunity.local', N'+27820000002', N'Example Pty', 1, 2, 0, N'Interested in Private', N'pricing', 0, GETUTCDATE());
END
GO

-- ============================================================================
-- 9. LEAD WORKFLOWS (sample actions)
-- ============================================================================
IF EXISTS (SELECT 1 FROM [LeadCaptureRequests] WHERE [Email] = N'lead.sample1@opportunity.local')
BEGIN
    DECLARE @LeadId1 INT = (SELECT [Id] FROM [LeadCaptureRequests] WHERE [Email] = N'lead.sample1@opportunity.local');
    IF NOT EXISTS (SELECT 1 FROM [LeadWorkflows] WHERE [LeadId] = @LeadId1)
    BEGIN
        INSERT INTO [LeadWorkflows] (
            [LeadId], [ActionType], [Notes], [PerformedBy], [Currency], [CreatedAt]
        ) VALUES
        (@LeadId1, 1, N'Callback scheduled with broker.', N'USR-BROKER-01', N'ZAR', GETUTCDATE());
    END
END
GO
