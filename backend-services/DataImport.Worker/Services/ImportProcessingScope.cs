using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using DataImport.Worker.Models;
using ClosedXML.Excel;

namespace DataImport.Worker.Services
{
    public class ImportProcessingScope : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly PropertyDbContext _propertyDbContext;
        private readonly IConfiguration _configuration;

        public ImportProcessingScope(ILogger logger = null, IConfiguration configuration = null)
        {
            _logger = logger;
            _configuration = configuration;

            var propertyOptions = new DbContextOptionsBuilder<PropertyDbContext>()
                .UseSqlServer("Server=172.18.0.3,1433;Database=Property;Trusted_Connection=True;MultipleActiveResultSets=true")
                .Options;
            _propertyDbContext = new PropertyDbContext(propertyOptions);
        }

        public async Task<ImportJobMessage?> DeserializeJobAsync(string payload)
        {
            try
            {
                return JsonSerializer.Deserialize<ImportJobMessage>(payload);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[IMPORT]: Failed to deserialize job payload.");
                return null;
            }
        }

        public async Task UpdateJobStatusAsync(string jobId, string status)
        {
            var job = await _propertyDbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job != null)
            {
                job.Status = status;
                if (status == "completed" || status == "failed")
                    job.CompletedAt = DateTime.UtcNow;
                await _propertyDbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateJobCountsAsync(string jobId, int total, int processed, int success, int errors)
        {
            var job = await _propertyDbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job != null)
            {
                job.TotalRows = total;
                job.ProcessedRows = processed;
                job.SuccessCount = success;
                job.ErrorCount = errors;
                await _propertyDbContext.SaveChangesAsync();
            }
        }

        public async Task IncrementProcessedAsync(string jobId)
        {
            var job = await _propertyDbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job != null)
            {
                job.ProcessedRows++;
                await _propertyDbContext.SaveChangesAsync();
            }
        }

        public async Task CompleteJobAsync(string jobId, int successCount, int errorCount, List<string> errors)
        {
            var job = await _propertyDbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job != null)
            {
                job.Status = errorCount == 0 ? "completed" : "completed_with_errors";
                job.SuccessCount = successCount;
                job.ErrorCount = errorCount;
                job.ErrorsJson = JsonSerializer.Serialize(errors);
                job.CompletedAt = DateTime.UtcNow;
                await _propertyDbContext.SaveChangesAsync();
            }
        }

        public async Task<PropertyAsset?> GetPropertyByAddressAsync(string address, string tenantId)
        {
            return await _propertyDbContext.PropertyAssets
                .FirstOrDefaultAsync(p => p.CompleteAddress == address && p.TenantId == tenantId);
        }

        public async Task ImportPropertyRowAsync(Dictionary<string, string?> row, string tenantId)
        {
            var property = new PropertyAsset
            {
                TenantId = tenantId,
                CompleteAddress = row.GetValueOrDefault("Address") ?? string.Empty,
                ErfSpatialSize = row.GetValueOrDefault("ErfSize") ?? string.Empty,
                TitleDeedType = row.GetValueOrDefault("TitleDeedType") ?? string.Empty,
                PortfolioZoning = row.GetValueOrDefault("Zoning") ?? string.Empty,
                PropertyTypeId = int.TryParse(row.GetValueOrDefault("PropertyTypeId"), out var ptid) ? ptid : 1,
                ValuationPrice = decimal.TryParse(row.GetValueOrDefault("ValuationPrice"), out var vp) ? vp : 0,
                SalePrice = decimal.TryParse(row.GetValueOrDefault("SalePrice"), out var sp) ? sp : null,
                IsForSale = bool.TryParse(row.GetValueOrDefault("IsForSale"), out var ifs) && ifs,
                IsForRent = bool.TryParse(row.GetValueOrDefault("IsForRent"), out var ifr) && ifr,
                Latitude = decimal.TryParse(row.GetValueOrDefault("Latitude"), out var lat) ? lat : 0,
                Longitude = decimal.TryParse(row.GetValueOrDefault("Longitude"), out var lon) ? lon : 0,
                PropertyDescription = row.GetValueOrDefault("Description") ?? string.Empty,
                LinkedOwnerId = row.GetValueOrDefault("OwnerId") ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _propertyDbContext.PropertyAssets.Add(property);
            await _propertyDbContext.SaveChangesAsync();
        }

        public async Task ImportUnitRowAsync(Dictionary<string, string?> row, int propertyId)
        {
            var unit = new PropertyUnit
            {
                PropertyId = propertyId,
                UnitNumber = row.GetValueOrDefault("UnitNumber") ?? $"Unit {_propertyDbContext.PropertyUnits.Count(u => u.PropertyId == propertyId) + 1}",
                UnitDescription = row.GetValueOrDefault("UnitDescription") ?? string.Empty,
                NetRentableAreaSqM = decimal.TryParse(row.GetValueOrDefault("NetRentableAreaSqM"), out var area) ? area : 0,
                SpecificUnitRentalPrice = decimal.TryParse(row.GetValueOrDefault("SpecificUnitRentalPrice"), out var rp) ? rp : 0,
                SpecificUnitSalePrice = decimal.TryParse(row.GetValueOrDefault("SpecificUnitSalePrice"), out var sp) ? sp : 0,
                RentalRate = decimal.TryParse(row.GetValueOrDefault("RentalRate"), out var rr) ? rr : 0,
                RentEscalationPercentage = decimal.TryParse(row.GetValueOrDefault("RentEscalationPercentage"), out var re) ? re : 0,
                LeaseTermMonths = int.TryParse(row.GetValueOrDefault("LeaseTermMonths"), out var lt) ? lt : 12,
                UnitImagesJson = "[]",
                IsCurrentlyVacant = bool.TryParse(row.GetValueOrDefault("IsCurrentlyVacant"), out var vac) && vac,
                CreatedAt = DateTime.UtcNow
            };

            _propertyDbContext.PropertyUnits.Add(unit);
            await _propertyDbContext.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _propertyDbContext.DisposeAsync();
        }
    }
}
