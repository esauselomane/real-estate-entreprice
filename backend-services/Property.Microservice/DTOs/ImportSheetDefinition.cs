namespace Property.Microservice.DTOs;

public record ImportSheetColumn(string ColumnName, object? SampleValue);

public static class ImportSheetDefinition
{
    public static ImportSheetColumn[] Agencies =>
    [
        new("AgencyId", "AG-0001"),
        new("TenantId", "TEN-0001"),
        new("Name", "Agency 1"),
        new("TradingName", ""),
        new("RegistrationNumber", "REG-ABCDE"),
        new("CompanyLogoUrl", ""),
        new("ContactEmail", "agency1@realestate.local"),
        new("HeadquartersAddress", "1 Harbour Road, Cape Town, South Africa"),
        new("OperationalStatus", "Active"),
        new("BrokerageLicense", ""),
        new("ParentAgencyId", ""),
        new("IsTrial", false),
        new("TrialStartDate", ""),
        new("TrialEndDate", "")
    ];

    public static ImportSheetColumn[] Brokers =>
    [
        new("BrokerId", "BRK-0001"),
        new("TenantId", "TEN-0001"),
        new("AgencyId", "AG-0001"),
        new("FullName", "Broker 1"),
        new("Email", "broker1@realestate.local"),
        new("Phone", "+27123456789"),
        new("Role", "Broker"),
        new("Status", "Active"),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] Contacts =>
    [
        new("ContactId", "CON-0001"),
        new("TenantId", "TEN-0001"),
        new("ContactType", "Broker"),
        new("FullName", "Contact 1"),
        new("CompanyName", "Acme Corp"),
        new("EmailAddress", "contact1@pipeline.local"),
        new("PhoneNumber", "+27123456789"),
        new("PositionJobTitle", "Director"),
        new("PrimaryLocation", "Cape Town"),
        new("BudgetLimit", 1500000m),
        new("PropertyTypeInterests", "House,Apartment"),
        new("AreaInterestIds", "1,2,3"),
        new("Notes", "Looking in Sea Point"),
        new("AssociatedCompanyId", "COM-0001"),
        new("AssociatedLegalEntityId", "LGE-0001"),
        new("LinkedDealIds", ""),
        new("AgencyId", "AG-0001"),
        new("IsAgencyAdmin", false),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] Companies =>
    [
        new("CompanyId", "COM-0001"),
        new("TenantId", "TEN-0001"),
        new("BusinessName", "Company 1"),
        new("RegistrationNumber", "REG-00001"),
        new("VatNumber", "VAT-00001"),
        new("CorporateWebsite", "https://company1.example.com"),
        new("HeadOfficeAddress", "1 Main Road, Johannesburg, South Africa"),
        new("CorporateCreditLimit", 505000m),
        new("IndustryVertical", "Property Investment"),
        new("AssociatedLegalEntityId", "LGE-0001"),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] LegalEntities =>
    [
        new("LegalEntityId", "LGE-0001"),
        new("TenantId", "TEN-0001"),
        new("RegisteredLegalName", "Legal Entity 1"),
        new("AssociatedCompanyId", "COM-0001"),
        new("EntityTypeClassifier", "Private Company"),
        new("TaxRegistrationIndex", "TAX-00001"),
        new("DeedsOfficeIdentifier", "DO-00001"),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] Leads =>
    [
        new("TenantId", "TEN-0001"),
        new("ClientName", "Lead Client 1"),
        new("ContactEmail", "lead1@opportunity.local"),
        new("ContactPhone", "+27123456789"),
        new("LinkedContactId", "CON-0001"),
        new("AllocatedBrokerIds", "BRK-0001"),
        new("LeadStage", "Contacted"),
        new("PropertyInterestClass", "Residential Sale"),
        new("PreferredArea", "Cape Town"),
        new("LeadType", "ForSale"),
        new("EstimatedBudget", 162500m),
        new("LeadNotes", "Prospect reviewed location."),
        new("IsConvertedToDeal", false),
        new("CommissionType", "Percent"),
        new("CommissionValue", 3.5m),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] Deals =>
    [
        new("TenantId", "TEN-0001"),
        new("AllocatedBrokerIds", "BRK-0001"),
        new("DealStage", "DiscoveryNeedsAnalysis"),
        new("LinkedPropertyId", 1),
        new("BuyerClientId", "CON-0001"),
        new("SellerClientId", "CON-OT-0001"),
        new("TransactionValue", 515000m),
        new("DealNotes", "Deal advanced through underwriting."),
        new("LastStageUpdatedAt", ""),
        new("CreatedAt", ""),
        new("CommissionType", "Percent"),
        new("CommissionValue", 3.5m),
        new("RealisedCommission", ""),
        new("PreviousStage", ""),
        new("PropertyAddress", "123 Main Road, Cape Town")
    ];

    public static ImportSheetColumn[] Geography =>
    [
        new("Country", "South Africa"),
        new("Province", "Western Cape"),
        new("City", "Cape Town"),
        new("Suburb", "City Bowl"),
        new("Latitude", -33.9249),
        new("Longitude", 18.4207)
    ];

    public static ImportSheetColumn[] Properties =>
    [
        new("PropertyId", ""),
        new("TenantId", "TEN-0001"),
        new("CompleteAddress", "123 Main Road, Cape Town"),
        new("ErfSpatialSize", "250 m²"),
        new("TitleDeedType", "Freehold"),
        new("PortfolioZoning", "Residential Zone"),
        new("MarketClass", "Sale"),
        new("IsForSale", true),
        new("IsForRent", false),
        new("PropertyTypeId", 1),
        new("ValuationPrice", 850000m),
        new("SalePrice", 925000m),
        new("TenantInstallationAllowance", 50000m),
        new("Latitude", -33.9249),
        new("Longitude", 18.4241),
        new("VerificationDocumentsJson", "[]"),
        new("VerificationDocumentMetadataJson", "[]"),
        new("GalleryImageJson", "[]"),
        new("LinkedOwnerId", "OT-0001"),
        new("PropertyDescription", "Beautiful family home"),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] Units =>
    [
        new("PropertyRef", "123 Main Road, Cape Town"),
        new("UnitNumber", "Unit 1"),
        new("UnitDescription", "Suite 1"),
        new("NetRentableAreaSqM", 95m),
        new("SpecificUnitRentalPrice", 7500m),
        new("SpecificUnitSalePrice", 1800000m),
        new("RentalRate", 7000m),
        new("RentEscalationPercentage", 7.5m),
        new("LeaseTermMonths", 12),
        new("UnitImagesJson", "[]"),
        new("UnitDocumentsJson", "[]"),
        new("IsCurrentlyVacant", true),
        new("CreatedAt", "")
    ];

    public static ImportSheetColumn[] PropertyTypes =>
    [
        new("Name", "Residential")
    ];

    public static ImportSheetColumn[] AmenityTypes =>
    [
        new("Name", "Parking"),
        new("RequiresBoolean", false),
        new("RequiresNumber", true),
        new("IsResidentialOnly", false),
        new("Label", "Covered Parking"),
        new("IsActive", true)
    ];

    public static ImportSheetColumn[] TenantAgencies =>
    [
        new("TenantId", "TEN-0001"),
        new("AgencyName", "Agency 1"),
        new("LogoInitials", "A1"),
        new("ContactEmail", "agency1@realestate.local"),
        new("HeadquartersAddress", "1 Harbour Road, Cape Town, South Africa"),
        new("OperationalStatus", "Active"),
        new("CreatedAt", ""),
        new("BrokerageLicense", ""),
        new("ParentTenantId", ""),
        new("PeachCustomerId", ""),
        new("SubscriptionStatus", "Trialing"),
        new("SubscriptionEndsAt", ""),
        new("ActiveSubscriptionId", ""),
        new("IsTrial", false),
        new("TrialStartDate", ""),
        new("TrialEndDate", "")
    ];

    public static ImportSheetColumn[] UserAccounts =>
    [
        new("UserId", "USR-0001"),
        new("TenantId", "TEN-0001"),
        new("FullName", "John Doe"),
        new("Email", "john@example.com"),
        new("PasswordHash", "Password123!"),
        new("SystemRole", "PlatformAdmin"),
        new("AvatarUrl", ""),
        new("AccountStatus", "Active"),
        new("LicenseNumber", ""),
        new("LicenseExpiry", ""),
        new("IsTrial", false),
        new("TrialStartDate", ""),
        new("TrialEndDate", ""),
        new("CreatedAt", "")
    ];

    private static readonly Dictionary<string, ImportSheetColumn[]> _sheets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Agencies"] = Agencies,
        ["Brokers"] = Brokers,
        ["Contacts"] = Contacts,
        ["Companies"] = Companies,
        ["LegalEntities"] = LegalEntities,
        ["Leads"] = Leads,
        ["Deals"] = Deals,
        ["Geography"] = Geography,
        ["Properties"] = Properties,
        ["Units"] = Units,
        ["PropertyTypes"] = PropertyTypes,
        ["AmenityTypes"] = AmenityTypes,
        ["TenantAgencies"] = TenantAgencies,
        ["UserAccounts"] = UserAccounts
    };

    public static ImportSheetColumn[]? TryGetSheet(string sheetName)
    {
        return _sheets.TryGetValue(sheetName, out var columns) ? columns : null;
    }

    public static IEnumerable<string> AllSheetNames => _sheets.Keys;
}
