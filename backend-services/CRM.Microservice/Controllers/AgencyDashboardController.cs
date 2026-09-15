using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Microservice.Data;
using CRM.Microservice.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;

namespace CRM.Microservice.Controllers
{
    /// <summary>
    /// Agency-wide dashboard endpoints — provides a consolidated "Agency at a Glance"
    /// view with KPI counts, growth charts, and tab data for admin users.
    /// Extends CRMControllerBase for shared tenant/role resolution and repositories.
    /// </summary>
    [ApiController]
    [Route("api/crm/agency")]
    public class AgencyDashboardController : CRMControllerBase
    {
        private readonly IUserGrowthRepository _userGrowthRepository;
        private readonly IPropertyCountService _propertyCountService;
        private readonly IContactPersonRepository _contactPersonRepository;

        public AgencyDashboardController(
            CRMDbContext dbContext,
            IHttpClientFactory http,
            IConfiguration configuration,
            ILogger<AgencyDashboardController> logger,
            PermissionCheckService permissionCheck,
            IAgencyRepository agencyRepository,
            IBrokerRepository brokerRepository,
            ILeadRepository leadRepository,
            IDealRepository dealRepository,
            ICompanyRepository companyRepository,
            ILegalEntityRepository legalEntityRepository,
            IContactRepository contactRepository,
            IGeographyRepository geographyRepository,
            IOperationalRecordRepository operationalRepository,
            IBrochureCardRepository brochureCardRepository,
            IRepository<BrochureCardLine> brochureCardLineRepository,
            IDealBrokerSplitRepository dealBrokerSplitRepository,
            INotificationRepository notificationRepository,
            IContactPersonRepository contactPersonRepository,
            IUserGrowthRepository userGrowthRepository,
            IPropertyCountService propertyCountService,
            IRepository<BrokerMonthlyTarget> brokerTargetRepository)
            : base(dbContext, http, configuration, logger, permissionCheck,
                   agencyRepository, brokerRepository, leadRepository, dealRepository,
                   companyRepository, legalEntityRepository, contactRepository,
                   geographyRepository, operationalRepository, brochureCardRepository,
                   brochureCardLineRepository, dealBrokerSplitRepository, notificationRepository, brokerTargetRepository)
        {
            _contactPersonRepository = contactPersonRepository;
            _userGrowthRepository = userGrowthRepository;
            _propertyCountService = propertyCountService;
        }

        // 📊 GET: agency/glance/summary — KPI counts for the current tenant scope
        [HttpGet("glance/summary")]
        public async Task<IActionResult> GetAgencyGlanceSummary([FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            if (visibleTenants.Count == 0)
                return Ok(new AgencyGlanceSummary());

            var totalBrokers = await BrokerRepository.CountAsync(b => visibleTenants.Contains(b.TenantId));
            var totalLeads = await LeadRepository.CountAsync(l => visibleTenants.Contains(l.TenantId));
            var totalDeals = await DealRepository.CountAsync(d => visibleTenants.Contains(d.TenantId));
            var closedDeals = await DealRepository.CountAsync(d => d.DealStage == DealWorkflowStage.ClosedWon && visibleTenants.Contains(d.TenantId));

            var closedRecords = await DealRepository.Query().Where(deal => visibleTenants.Contains(deal.TenantId) && deal.DealStage == DealWorkflowStage.ClosedWon).ToListAsync();
            var totalRealisedCommission = closedRecords.Sum(Services.Reporting.CommissionCalculator.Realised);

            var soleMandateCount = await _propertyCountService.GetTotalSoleMandatesAsync(visibleTenants, HttpContext);

            var summary = new AgencyGlanceSummary
            {
                TotalUsers = totalBrokers,
                TotalBrokers = totalBrokers,
                TotalLeads = totalLeads,
                TotalDeals = totalDeals,
                TotalPropertiesWithSoleMandates = soleMandateCount,
                TotalRealisedCommission = totalRealisedCommission,
                ClosedDeals = closedDeals
            };

            Logger.LogInformation("GetAgencyGlanceSummary: tenants={TenantCount} brokers={Brokers} leads={Leads} deals={Deals} revenue={Revenue}",
                visibleTenants.Count, totalBrokers, totalLeads, totalDeals, totalRealisedCommission);

            return Ok(summary);
        }

        // 📋 GET: agency/glance/tabs — tab data: agency details, contacts, subscriptions, charts
        [HttpGet("glance/tabs")]
        public async Task<IActionResult> GetAgencyGlanceTabs([FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            if (visibleTenants.Count == 0)
                return Ok(new AgencyGlanceTabData());

            var tenantIdForData = visibleTenants.First();

            // Agency details (first visible agency)
            var agency = await AgencyRepository.Query()
                .AsNoTracking()
                .Where(a => a.TenantId == tenantIdForData)
                .FirstOrDefaultAsync();

            var agencyDetails = new AgencyDetailsDto
            {
                AgencyId = agency?.AgencyId ?? string.Empty,
                TenantId = agency?.TenantId ?? string.Empty,
                Name = agency?.Name ?? string.Empty,
                TradingName = agency?.TradingName ?? string.Empty,
                RegistrationNumber = agency?.RegistrationNumber ?? string.Empty,
                ContactEmail = agency?.ContactEmail ?? string.Empty,
                HeadquartersAddress = agency?.HeadquartersAddress ?? string.Empty,
                OperationalStatus = agency?.OperationalStatus ?? string.Empty,
                BrokerageLicense = agency?.BrokerageLicense ?? string.Empty,
                CreatedAt = agency?.CreatedAt ?? DateTime.UtcNow
            };

            // Contact persons for the agency
            var contactPersons = await _contactPersonRepository.Query()
                .AsNoTracking()
                .Where(c => c.AgencyId == agencyDetails.AgencyId)
                .Select(c => new ContactPersonDto
                {
                    FullName = c.FullName,
                    EmailAddress = c.Email,
                    PhoneNumber = c.Phone,
                    PositionJobTitle = c.Position,
                    ContactType = c.Role,
                    IsAgencyAdmin = c.IsAgencyAdmin
                })
                .ToListAsync();

            // Subscription packages — active and past
            var (activeSubs, pastSubs) = await _userGrowthRepository.GetSubscriptionHistoryAsync(tenantIdForData);

            // Growth charts
            var usersOverTime = (await _userGrowthRepository.GetUsersGrowthOver6MonthsAsync(visibleTenants)).ToList();
            var propertiesOverTime = (await _propertyCountService.GetPropertiesGrowthOver6MonthsAsync(visibleTenants, HttpContext)).ToList();

            var tabData = new AgencyGlanceTabData
            {
                AgencyDetails = agencyDetails,
                ContactPersons = contactPersons,
                ActiveSubscriptions = activeSubs,
                PastSubscriptions = pastSubs,
                UsersOverTime = usersOverTime,
                PropertiesOverTime = propertiesOverTime
            };

            return Ok(tabData);
        }
    }
}
