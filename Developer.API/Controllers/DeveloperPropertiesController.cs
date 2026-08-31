using Developer.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/properties")]
    public class DeveloperPropertiesController : ControllerBase
    {
        private readonly PropertyReadDbContext _propertyDb;

        public DeveloperPropertiesController(PropertyReadDbContext propertyDb)
        {
            _propertyDb = propertyDb;
        }

        [HttpGet]
        public async Task<IActionResult> GetProperties()
        {
            if (!HttpContext.Items.TryGetValue("AgencyId", out var agencyIdObj) || agencyIdObj is not string agencyId)
            {
                return Unauthorized(new { message = "A valid API key is required." });
            }

            var properties = await _propertyDb.PropertyAssets
                .AsNoTracking()
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
                    BaseRentalRate = p.BaseRentalRate,
                    RentEscalationPercentage = p.RentEscalationPercentage,
                    LeaseTermMonths = p.LeaseTermMonths,
                    TenantInstallationAllowance = p.TenantInstallationAllowance,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    LinkedOwnerId = p.LinkedOwnerId,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

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
                return Unauthorized(new { message = "A valid API key is required." });
            }

            var property = await _propertyDb.PropertyAssets
                .AsNoTracking()
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
                BaseRentalRate = property.BaseRentalRate,
                RentEscalationPercentage = property.RentEscalationPercentage,
                LeaseTermMonths = property.LeaseTermMonths,
                TenantInstallationAllowance = property.TenantInstallationAllowance,
                Latitude = property.Latitude,
                Longitude = property.Longitude,
                LinkedOwnerId = property.LinkedOwnerId,
                CreatedAt = property.CreatedAt
            };

            return Ok(dto);
        }
    }
}
