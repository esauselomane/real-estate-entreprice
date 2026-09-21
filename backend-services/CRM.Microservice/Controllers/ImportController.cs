using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CRM.Microservice.Data;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Controllers;

[ApiController]
[Authorize]
[Route("api/crm")]
public class ImportController : ControllerBase
{
    private readonly CRMDbContext _dbContext;
    private readonly ILogger<ImportController> _logger;

    public ImportController(CRMDbContext dbContext, ILogger<ImportController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportBatch([FromBody] CrmBatchImportRequest request, CancellationToken ct = default)
    {
        if (!IsPlatformAdmin(User))
        {
            _logger.LogWarning("ImportBatch rejected: caller is not a platform administrator.");
            return Forbid();
        }

        var results = new Dictionary<string, BatchSheetResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheetEntry in request.Sheets)
        {
            var errors = new List<string>();
            var imported = ImportSheet(sheetEntry.Key, sheetEntry.Value, request.TenantId, errors);
            results[sheetEntry.Key] = new BatchSheetResult(imported, errors.Count, errors);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("CRM batch import completed for tenant {TenantId} across {SheetCount} sheets.", request.TenantId, request.Sheets.Count);

        return Ok(new CrmBatchImportResponse { Sheets = results });
    }

    private static bool IsPlatformAdmin(System.Security.Claims.ClaimsPrincipal user)
    {
        var role = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                   ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return role == "PlatformAdmin" || role == "SuperAdmin";
    }

    private int ImportSheet(string sheetName, List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        return sheetName switch
        {
            "Agencies" => ImportAgencies(rows, fallbackTenantId, errors),
            "Brokers" => ImportBrokers(rows, fallbackTenantId, errors),
            "Contacts" => ImportContacts(rows, fallbackTenantId, errors),
            "Companies" => ImportCompanies(rows, fallbackTenantId, errors),
            "LegalEntities" => ImportLegalEntities(rows, fallbackTenantId, errors),
            "Leads" => ImportLeads(rows, fallbackTenantId, errors),
            "Deals" => ImportDeals(rows, fallbackTenantId, errors),
            "Geography" => ImportGeography(rows, errors),
            _ => 0
        };
    }

    private int ImportAgencies(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var agency = new AgencyRecord
                {
                    AgencyId = row.GetValueOrDefault("AgencyId") ?? string.Empty,
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    Name = row.GetValueOrDefault("Name") ?? string.Empty,
                    TradingName = row.GetValueOrDefault("TradingName") ?? string.Empty,
                    RegistrationNumber = row.GetValueOrDefault("RegistrationNumber") ?? string.Empty,
                    CompanyLogoUrl = row.GetValueOrDefault("CompanyLogoUrl") ?? string.Empty,
                    ContactEmail = row.GetValueOrDefault("ContactEmail") ?? string.Empty,
                    HeadquartersAddress = row.GetValueOrDefault("HeadquartersAddress") ?? string.Empty,
                    OperationalStatus = row.GetValueOrDefault("OperationalStatus") ?? "Active",
                    BrokerageLicense = row.GetValueOrDefault("BrokerageLicense") ?? string.Empty,
                    ParentAgencyId = ParseNullableString(row.GetValueOrDefault("ParentAgencyId")),
                    IsTrial = ParseBool(row.GetValueOrDefault("IsTrial")),
                    TrialStartDate = ParseDateTime(row.GetValueOrDefault("TrialStartDate")),
                    TrialEndDate = ParseDateTime(row.GetValueOrDefault("TrialEndDate")),
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(agency.AgencyId))
                {
                    agency.AgencyId = "AG-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                if (string.IsNullOrWhiteSpace(agency.RegistrationNumber))
                {
                    agency.RegistrationNumber = "REG-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                _dbContext.AgencyRecords.Add(agency);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Agency row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportBrokers(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var broker = new BrokerRecord
                {
                    BrokerId = row.GetValueOrDefault("BrokerId") ?? string.Empty,
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    AgencyId = row.GetValueOrDefault("AgencyId") ?? string.Empty,
                    FullName = row.GetValueOrDefault("FullName") ?? string.Empty,
                    Email = row.GetValueOrDefault("Email") ?? string.Empty,
                    Phone = row.GetValueOrDefault("Phone") ?? string.Empty,
                    Role = row.GetValueOrDefault("Role") ?? "Broker",
                    Status = row.GetValueOrDefault("Status") ?? "Active",
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(broker.BrokerId))
                {
                    broker.BrokerId = "BRK-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                _dbContext.BrokerRecords.Add(broker);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Broker row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportCompanies(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var company = new CRMCompany
                {
                    CompanyId = row.GetValueOrDefault("CompanyId") ?? string.Empty,
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    BusinessName = row.GetValueOrDefault("BusinessName") ?? string.Empty,
                    RegistrationNumber = row.GetValueOrDefault("RegistrationNumber") ?? string.Empty,
                    VatNumber = row.GetValueOrDefault("VatNumber") ?? string.Empty,
                    CorporateWebsite = row.GetValueOrDefault("CorporateWebsite") ?? string.Empty,
                    HeadOfficeAddress = row.GetValueOrDefault("HeadOfficeAddress") ?? string.Empty,
                    CorporateCreditLimit = ParseDecimal(row.GetValueOrDefault("CorporateCreditLimit")),
                    IndustryVertical = row.GetValueOrDefault("IndustryVertical") ?? "Real Estate Investment",
                    AssociatedLegalEntityId = ParseNullableString(row.GetValueOrDefault("AssociatedLegalEntityId")),
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(company.CompanyId))
                {
                    company.CompanyId = "COM-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                _dbContext.CRMCompanies.Add(company);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Company row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportLegalEntities(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var entity = new CRMLegalEntity
                {
                    LegalEntityId = row.GetValueOrDefault("LegalEntityId") ?? string.Empty,
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    RegisteredLegalName = row.GetValueOrDefault("RegisteredLegalName") ?? string.Empty,
                    AssociatedCompanyId = ParseNullableString(row.GetValueOrDefault("AssociatedCompanyId")),
                    EntityTypeClassifier = row.GetValueOrDefault("EntityTypeClassifier") ?? "Private Company",
                    TaxRegistrationIndex = row.GetValueOrDefault("TaxRegistrationIndex") ?? string.Empty,
                    DeedsOfficeIdentifier = row.GetValueOrDefault("DeedsOfficeIdentifier") ?? string.Empty,
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(entity.LegalEntityId))
                {
                    entity.LegalEntityId = "LGE-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                _dbContext.CRMLegalEntities.Add(entity);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"LegalEntity row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportContacts(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var contact = new Contact
                {
                    ContactId = row.GetValueOrDefault("ContactId") ?? string.Empty,
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    ContactType = ContactTypeParser.Parse(row.GetValueOrDefault("ContactType") ?? string.Empty),
                    FullName = row.GetValueOrDefault("FullName") ?? string.Empty,
                    CompanyName = ParseNullableString(row.GetValueOrDefault("CompanyName")),
                    EmailAddress = row.GetValueOrDefault("EmailAddress") ?? string.Empty,
                    PhoneNumber = row.GetValueOrDefault("PhoneNumber") ?? string.Empty,
                    PositionJobTitle = ParseNullableString(row.GetValueOrDefault("PositionJobTitle")),
                    PrimaryLocation = ParseNullableString(row.GetValueOrDefault("PrimaryLocation")),
                    BudgetLimit = ParseDecimal(row.GetValueOrDefault("BudgetLimit")),
                    PropertyTypeInterests = ParseNullableString(row.GetValueOrDefault("PropertyTypeInterests")),
                    AreaInterestIds = ParseNullableString(row.GetValueOrDefault("AreaInterestIds")),
                    Notes = ParseNullableString(row.GetValueOrDefault("Notes")),
                    AssociatedCompanyId = ParseNullableString(row.GetValueOrDefault("AssociatedCompanyId")),
                    AssociatedLegalEntityId = ParseNullableString(row.GetValueOrDefault("AssociatedLegalEntityId")),
                    LinkedDealIds = ParseNullableString(row.GetValueOrDefault("LinkedDealIds")),
                    AgencyId = ParseNullableString(row.GetValueOrDefault("AgencyId")),
                    IsAgencyAdmin = ParseBool(row.GetValueOrDefault("IsAgencyAdmin")),
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(contact.ContactId))
                {
                    contact.ContactId = "CON-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                _dbContext.Contacts.Add(contact);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Contact row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportLeads(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var lead = new LeadRecord
                {
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    ClientName = row.GetValueOrDefault("ClientName") ?? string.Empty,
                    ContactEmail = row.GetValueOrDefault("ContactEmail") ?? string.Empty,
                    ContactPhone = row.GetValueOrDefault("ContactPhone") ?? string.Empty,
                    LinkedContactId = ParseNullableString(row.GetValueOrDefault("LinkedContactId")),
                    AllocatedBrokerIds = row.GetValueOrDefault("AllocatedBrokerIds") ?? string.Empty,
                    LeadStage = ParseEnum(row.GetValueOrDefault("LeadStage"), LeadWorkflowStage.Contacted),
                    PropertyInterestClass = row.GetValueOrDefault("PropertyInterestClass") ?? string.Empty,
                    PreferredArea = row.GetValueOrDefault("PreferredArea") ?? string.Empty,
                    LeadType = row.GetValueOrDefault("LeadType") ?? "ForSale",
                    EstimatedBudget = ParseDecimal(row.GetValueOrDefault("EstimatedBudget")),
                    LeadNotes = row.GetValueOrDefault("LeadNotes") ?? string.Empty,
                    IsConvertedToDeal = ParseBool(row.GetValueOrDefault("IsConvertedToDeal")),
                    CommissionType = row.GetValueOrDefault("CommissionType") ?? CommissionTypes.Percent,
                    CommissionValue = ParseDecimal(row.GetValueOrDefault("CommissionValue")),
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                };

                _dbContext.LeadRecords.Add(lead);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Lead row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportDeals(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var deal = new DealRecord
                {
                    TenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId,
                    AllocatedBrokerIds = row.GetValueOrDefault("AllocatedBrokerIds") ?? string.Empty,
                    DealStage = ParseEnum(row.GetValueOrDefault("DealStage"), DealWorkflowStage.DiscoveryNeedsAnalysis),
                    LinkedPropertyId = ParseNullableInt(row.GetValueOrDefault("LinkedPropertyId")),
                    BuyerClientId = row.GetValueOrDefault("BuyerClientId") ?? string.Empty,
                    SellerClientId = row.GetValueOrDefault("SellerClientId") ?? string.Empty,
                    TransactionValue = ParseDecimal(row.GetValueOrDefault("TransactionValue")),
                    DealNotes = row.GetValueOrDefault("DealNotes") ?? string.Empty,
                    LastStageUpdatedAt = ParseDateTime(row.GetValueOrDefault("LastStageUpdatedAt")) ?? DateTime.UtcNow,
                    CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow,
                    CommissionType = row.GetValueOrDefault("CommissionType") ?? CommissionTypes.Percent,
                    CommissionValue = ParseDecimal(row.GetValueOrDefault("CommissionValue")),
                    RealisedCommission = ParseNullableDecimal(row.GetValueOrDefault("RealisedCommission")),
                    PreviousStage = row.GetValueOrDefault("PreviousStage") ?? string.Empty,
                    PropertyAddress = row.GetValueOrDefault("PropertyAddress") ?? string.Empty
                };

                _dbContext.DealRecords.Add(deal);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Deal row: {ex.Message}");
            }
        }

        return imported;
    }

    private int ImportGeography(List<Dictionary<string, string>> rows, List<string> errors)
    {
        var imported = 0;
        foreach (var row in rows)
        {
            try
            {
                var geography = new GeographyRecord
                {
                    Country = row.GetValueOrDefault("Country") ?? "South Africa",
                    Province = row.GetValueOrDefault("Province") ?? string.Empty,
                    City = row.GetValueOrDefault("City") ?? string.Empty,
                    Suburb = ParseNullableString(row.GetValueOrDefault("Suburb")),
                    Latitude = ParseDecimal(row.GetValueOrDefault("Latitude")),
                    Longitude = ParseDecimal(row.GetValueOrDefault("Longitude"))
                };

                _dbContext.GeographyRecords.Add(geography);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Geography row: {ex.Message}");
            }
        }

        return imported;
    }

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return Enum.TryParse<T>(value, true, out var parsed) ? parsed : defaultValue;
    }

    private static string? ParseNullableString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static int ParseInt(string? value, int defaultValue = 0)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static decimal ParseDecimal(string? value, decimal defaultValue = 0m)
    {
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        return decimal.TryParse(value, out var result) ? result : null;
    }

    private static bool ParseBool(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(value, out var result) ? result : null;
    }
}
