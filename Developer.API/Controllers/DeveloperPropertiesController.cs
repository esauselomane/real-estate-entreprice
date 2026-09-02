using Developer.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/properties")]
    public class DeveloperPropertiesController : ControllerBase
    {
        private readonly PropertyReadDbContext _propertyDb;
        private readonly ILogger<DeveloperPropertiesController> _logger;

        public DeveloperPropertiesController(PropertyReadDbContext propertyDb, ILogger<DeveloperPropertiesController> logger)
        {
            _propertyDb = propertyDb;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProperties()
        {
            if (!HttpContext.Items.TryGetValue("AgencyId", out var agencyIdObj) || agencyIdObj is not string agencyId)
            {
                _logger.LogWarning("GetProperties: unauthorized — no API key / AgencyId in context");
                return Unauthorized(new { message = "A valid API key is required." });
            }

            _logger.LogInformation("GetProperties: AgencyId={AgencyId}", agencyId);
            var properties = await _propertyDb.PropertyAssets
                .AsNoTracking()
                .Include(p => p.SubUnits)
                .Where(p => p.TenantId == agencyId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new DeveloperPropertyDto
                {
                    PropertyId = p.PropertyId,
                    TenantId = p.TenantId,
                    CompleteAddress = p.CompleteAddress,
                    ErfSpatialSize = p.ErfSpatialSize,
                    TitleDeedType = p.TitleDeedType,
                    PortfolioZoning = p.PortfolioZoning,
                    MarketClass = p.MarketClass,
                    IsForSale = p.IsForSale,
                    IsForRent = p.IsForRent,
                    PropertyTypeId = p.PropertyTypeId,
                    ValuationPrice = p.ValuationPrice,
                    SalePrice = p.SalePrice,
                    TenantInstallationAllowance = p.TenantInstallationAllowance,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    LinkedOwnerId = p.LinkedOwnerId,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation("GetProperties: returned {Count} properties for AgencyId={AgencyId}", properties.Count, agencyId);
            return Ok(new
            {
                agencyId,
                count = properties.Count,
                properties
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProperty(int id)
        {
            if (!HttpContext.Items.TryGetValue("AgencyId", out var agencyIdObj) || agencyIdObj is not string agencyId)
            {
                _logger.LogWarning("GetProperty: unauthorized — no API key / AgencyId in context");
                return Unauthorized(new { message = "A valid API key is required." });
            }

            _logger.LogInformation("GetProperty: PropertyId={PropertyId} AgencyId={AgencyId}", id, agencyId);
            var property = await _propertyDb.PropertyAssets
                .AsNoTracking()
                .Include(p => p.SubUnits)
                .FirstOrDefaultAsync(p => p.PropertyId == id && p.TenantId == agencyId);

            if (property == null)
                return NotFound(new { message = "Property not found or access denied." });

            var dto = new DeveloperPropertyDto
            {
                PropertyId = property.PropertyId,
                TenantId = property.TenantId,
                CompleteAddress = property.CompleteAddress,
                ErfSpatialSize = property.ErfSpatialSize,
                TitleDeedType = property.TitleDeedType,
                PortfolioZoning = property.PortfolioZoning,
                MarketClass = property.MarketClass,
                IsForSale = property.IsForSale,
                IsForRent = property.IsForRent,
                PropertyTypeId = property.PropertyTypeId,
                ValuationPrice = property.ValuationPrice,
                SalePrice = property.SalePrice,
                TenantInstallationAllowance = property.TenantInstallationAllowance,
                Latitude = property.Latitude,
                Longitude = property.Longitude,
                LinkedOwnerId = property.LinkedOwnerId,
                CreatedAt = property.CreatedAt
            };

            _logger.LogInformation("GetProperty: found PropertyId={PropertyId}", id);
            return Ok(dto);
        }
    }
}
