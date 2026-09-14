using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Property.Microservice.Services.BackgroundJobs
{
    public class DataImportBackgroundService : BackgroundService
    {
        private readonly ILogger<DataImportBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IJobQueue<DataImportJob> _jobQueue;

        public DataImportBackgroundService(
            ILogger<DataImportBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IJobQueue<DataImportJob> jobQueue)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobQueue = jobQueue;
        }

        public async ValueTask EnqueueAsync(DataImportJob job, CancellationToken ct = default)
        {
            await _jobQueue.EnqueueAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DATA IMPORT BACKGROUND SERVICE]: Started, listening for import jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("[DATA IMPORT]: Processing job {JobId}", job.JobId);

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();

                    await ProcessImportJobAsync(job, dbContext, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DATA IMPORT]: Error processing job");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessImportJobAsync(DataImportJob job, PropertyDbContext dbContext, CancellationToken ct)
        {
            var bytes = Convert.FromBase64String(job.Base64Content);
            using var stream = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(stream);

            var sheets = new Dictionary<string, List<Dictionary<string, string?>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var ws in workbook.Worksheets)
            {
                var rows = new List<Dictionary<string, string?>>();
                var headerRow = ws.Row(1);
                var headers = headerRow.CellsUsed().Select(c => c.GetString()).ToList();
                for (var r = 2; r <= ws.LastRowUsed().RowNumber(); r++)
                {
                    if (ct.IsCancellationRequested) break;
                    var row = ws.Row(r);
                    var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    for (var c = 1; c <= headers.Count; c++)
                    {
                        dict[headers[c - 1]] = row.Cell(c).GetString();
                    }
                    rows.Add(dict);
                }
                sheets[ws.Name] = rows;
            }

            var propertyAddressToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var successCount = 0;
            var errors = new List<string>();

            // 1. Import properties
            if (sheets.TryGetValue("Properties", out var propertyRows))
            {
                foreach (var row in propertyRows)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var prop = new PropertyAsset
                        {
                            TenantId = job.TenantId,
                            PropertyDescription = row.GetValueOrDefault("Title") ?? row.GetValueOrDefault("Description") ?? string.Empty,
                            PropertyTypeId = int.TryParse(row.GetValueOrDefault("PropertyTypeId"), out var pt) ? pt : 1,
                            CompleteAddress = row.GetValueOrDefault("Address") ?? string.Empty,
                            Latitude = decimal.TryParse(row.GetValueOrDefault("Latitude"), out var lat) ? lat : 0,
                            Longitude = decimal.TryParse(row.GetValueOrDefault("Longitude"), out var lng) ? lng : 0,
                            ValuationPrice = decimal.TryParse(row.GetValueOrDefault("Price"), out var p) ? p : 0,
                            ErfSpatialSize = row.GetValueOrDefault("AreaSqm") ?? string.Empty,
                            TitleDeedType = row.GetValueOrDefault("MandateType") ?? "Sole",
                            PortfolioZoning = row.GetValueOrDefault("Zoning") ?? string.Empty,
                            MarketClass = row.GetValueOrDefault("MarketClass") ?? "Sale",
                            SalePrice = decimal.TryParse(row.GetValueOrDefault("SalePrice"), out var sp) ? sp : null,
                            IsForSale = row.GetValueOrDefault("IsForSale") == "true" || row.GetValueOrDefault("MarketClass") == "Sale",
                            IsForRent = row.GetValueOrDefault("IsForRent") == "true" || row.GetValueOrDefault("MarketClass") == "Rental",
                            CreatedAt = DateTime.UtcNow
                        };
                        dbContext.PropertyAssets.Add(prop);
                        await dbContext.SaveChangesAsync(ct);
                        propertyAddressToId[prop.CompleteAddress] = prop.PropertyId;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Property row: {ex.Message}");
                    }
                }
            }

            // 2. Import units
            if (sheets.TryGetValue("Units", out var unitRows))
            {
                foreach (var row in unitRows)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var propertyRef = row.GetValueOrDefault("PropertyRef") ?? string.Empty;
                        if (propertyAddressToId.TryGetValue(propertyRef, out var propertyId))
                        {
                            var unit = new PropertyUnit
                            {
                                PropertyId = propertyId,
                                UnitNumber = row.GetValueOrDefault("UnitNumber") ?? string.Empty,
                                UnitDescription = row.GetValueOrDefault("UnitDescription") ?? string.Empty,
                                NetRentableAreaSqM = decimal.TryParse(row.GetValueOrDefault("NetRentableAreaSqM"), out var nra) ? nra : 0,
                                RentalRate = decimal.TryParse(row.GetValueOrDefault("RentalRate"), out var rr) ? rr : null,
                                RentEscalationPercentage = decimal.TryParse(row.GetValueOrDefault("RentEscalationPercentage"), out var rep) ? rep : null,
                                LeaseTermMonths = int.TryParse(row.GetValueOrDefault("LeaseTermMonths"), out var ltm) ? ltm : null,
                                SpecificUnitSalePrice = decimal.TryParse(row.GetValueOrDefault("SalePrice"), out var sp) ? sp : null,
                                SpecificUnitRentalPrice = decimal.TryParse(row.GetValueOrDefault("RentalRate"), out var srp) ? srp : null,
                                CreatedAt = DateTime.UtcNow
                            };
                            dbContext.PropertyUnits.Add(unit);
                            await dbContext.SaveChangesAsync(ct);
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"Unit row: property not found for '{propertyRef}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Unit row: {ex.Message}");
                    }
                }
            }

            // Create notification for import completion
            var notification = new Property.Microservice.Models.Notification
            {
                TenantId = job.TenantId,
                UserId = job.RequestedBy,
                Category = "import",
                Title = errors.Count == 0 ? "Data import completed" : "Data import completed with errors",
                Body = errors.Count == 0 
                    ? $"{job.FileName}: Import completed successfully." 
                    : $"{job.FileName}: Import completed with {errors.Count} errors.",
                LinkType = "import",
                LinkId = job.JobId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("[DATA IMPORT]: Job {JobId} finished — {Success} success, {Errors} errors", job.JobId, successCount, errors.Count);
        }
    }
}