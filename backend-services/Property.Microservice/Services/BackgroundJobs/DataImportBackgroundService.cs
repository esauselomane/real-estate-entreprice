using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Property.Microservice.Services.BackgroundJobs
{
    public class DataImportBackgroundService : BackgroundService
    {
        private readonly ILogger<DataImportBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IJobQueue<DataImportJob> _jobQueue;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private static readonly string[] CrmSheetNames = { "Agencies", "Brokers", "Contacts", "Companies", "LegalEntities", "Leads", "Deals", "Geography" };
        private static readonly string[] IdentitySheetNames = { "TenantAgencies", "UserAccounts" };

        public DataImportBackgroundService(
            ILogger<DataImportBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IJobQueue<DataImportJob> jobQueue,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobQueue = jobQueue;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async ValueTask EnqueueAsync(DataImportJob job, CancellationToken ct = default)
        {
            await _jobQueue.EnqueueAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DATA IMPORT]: started, listening for import jobs.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("[DATA IMPORT]: processing job {JobId}", job.JobId);

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();

                    await ProcessImportJobAsync(job, dbContext, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DATA IMPORT]: unhandled error processing job");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessImportJobAsync(DataImportJob job, PropertyDbContext dbContext, CancellationToken ct)
        {
            var progress = new ImportProgress();

            try
            {
                await UpdateTrackingJobAsync(dbContext, job.JobId, "processing", progress);

                var sheets = ReadWorkbookSheets(job.Base64Content);
                progress.TotalRows = CountDelegatedRows(sheets, CrmSheetNames)
                                  + CountDelegatedRows(sheets, IdentitySheetNames)
                                  + CountRows(sheets, "Properties")
                                  + CountRows(sheets, "Units")
                                  + CountRows(sheets, "PropertyTypes")
                                  + CountRows(sheets, "AmenityTypes");
                await UpdateTrackingJobAsync(dbContext, job.JobId, "processing", progress);

                await ImportPropertyCataloguesAsync(sheets, dbContext, progress, ct);

                var propertyAddressToId = await ImportPropertyTablesAsync(sheets, job.TenantId, dbContext, progress, ct);
                await ImportUnitsAsync(sheets, propertyAddressToId, job.TenantId, dbContext, progress, ct);

                await ImportCrmTablesAsync(sheets, job, progress, ct);
                await ImportIdentityTablesAsync(sheets, job, progress, ct);

                progress.FinalizeStatus();
                await UpdateTrackingJobAsync(dbContext, job.JobId, progress.Status, progress, completed: true);

                await CreateCompletionNotificationAsync(dbContext, job, progress);

                _logger.LogInformation(
                    "[DATA IMPORT]: job {JobId} finished — {Success} success, {Errors} errors",
                    job.JobId, progress.SuccessCount, progress.ErrorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DATA IMPORT]: job {JobId} failed", job.JobId);
                progress.Errors.Add($"Fatal: {ex.Message}");
                await UpdateTrackingJobAsync(dbContext, job.JobId, "failed", progress, completed: true);
            }
        }

        private static Dictionary<string, List<Dictionary<string, string>>> ReadWorkbookSheets(string base64Content)
        {
            var bytes = Convert.FromBase64String(base64Content);
            using var stream = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(stream);

            var sheets = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var ws in workbook.Worksheets)
            {
                sheets[ws.Name] = ReadWorksheetRows(ws);
            }

            return sheets;
        }

        private static List<Dictionary<string, string>> ReadWorksheetRows(IXLWorksheet ws)
        {
            var rows = new List<Dictionary<string, string>>();

            var headerRow = ws.Row(1);
            var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            if (lastColumn == 0)
                return rows;

            var headers = new List<string>();
            for (var column = 1; column <= lastColumn; column++)
                headers.Add(headerRow.Cell(column).GetValue<string>() ?? string.Empty);

            var lastDataRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastDataRow; rowNumber++)
            {
                var sourceRow = ws.Row(rowNumber);
                if (sourceRow.CellsUsed().Count() == 0)
                    continue;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var column = 1; column <= headers.Count; column++)
                    dict[headers[column - 1]] = sourceRow.Cell(column).GetValue<string>() ?? string.Empty;

                rows.Add(dict);
            }

            return rows;
        }

        private async Task<Dictionary<string, int>> ImportPropertyTablesAsync(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            string tenantId,
            PropertyDbContext dbContext,
            ImportProgress progress,
            CancellationToken ct)
        {
            var addressToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (!sheets.TryGetValue("Properties", out var propertyRows))
                return addressToId;

            foreach (var row in propertyRows)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var asset = BuildPropertyAsset(row, tenantId);
                    dbContext.PropertyAssets.Add(asset);
                    progress.RecordSuccess();

                    if (!string.IsNullOrWhiteSpace(asset.CompleteAddress))
                        addressToId[asset.CompleteAddress] = asset.PropertyId;
                }
                catch (Exception ex)
                {
                    progress.RecordError($"Property row: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync(ct);
            return addressToId;
        }

        private async Task ImportUnitsAsync(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            Dictionary<string, int> addressToId,
            string tenantId,
            PropertyDbContext dbContext,
            ImportProgress progress,
            CancellationToken ct)
        {
            if (!sheets.TryGetValue("Units", out var unitRows))
                return;

            foreach (var row in unitRows)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var propertyRef = SheetValueParser.Text(row, "PropertyRef");
                    if (!addressToId.TryGetValue(propertyRef, out var propertyId))
                    {
                        progress.RecordError($"Unit row: property not found for '{propertyRef}'.");
                        continue;
                    }

                    var unit = BuildPropertyUnit(row, propertyId, tenantId);
                    dbContext.PropertyUnits.Add(unit);
                    progress.RecordSuccess();
                }
                catch (Exception ex)
                {
                    progress.RecordError($"Unit row: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync(ct);
        }

        private async Task ImportPropertyCataloguesAsync(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            PropertyDbContext dbContext,
            ImportProgress progress,
            CancellationToken ct)
        {
            if (sheets.TryGetValue("PropertyTypes", out var typeRows))
            {
                foreach (var row in typeRows)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var name = SheetValueParser.Text(row, "Name");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            progress.RecordError("PropertyType row: Name is required.");
                            continue;
                        }

                        var exists = await dbContext.PropertyTypes.AnyAsync(t => t.Name == name, ct);
                        if (exists)
                        {
                            progress.RecordError($"PropertyType row: '{name}' already exists.");
                            continue;
                        }

                        dbContext.PropertyTypes.Add(new PropertyType { Name = name });
                        progress.RecordSuccess();
                    }
                    catch (Exception ex)
                    {
                        progress.RecordError($"PropertyType row: {ex.Message}");
                    }
                }

                await dbContext.SaveChangesAsync(ct);
            }

            if (sheets.TryGetValue("AmenityTypes", out var amenityRows))
            {
                foreach (var row in amenityRows)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var name = SheetValueParser.Text(row, "Name");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            progress.RecordError("AmenityType row: Name is required.");
                            continue;
                        }

                        var exists = await dbContext.AmenityTypes.AnyAsync(a => a.Name == name, ct);
                        if (exists)
                        {
                            progress.RecordError($"AmenityType row: '{name}' already exists.");
                            continue;
                        }

                        dbContext.AmenityTypes.Add(new AmenityType
                        {
                            Name = name,
                            RequiresBoolean = SheetValueParser.Bool(row, "RequiresBoolean"),
                            RequiresNumber = SheetValueParser.Bool(row, "RequiresNumber"),
                            IsResidentialOnly = SheetValueParser.Bool(row, "IsResidentialOnly"),
                            Label = SheetValueParser.NullableText(row, "Label"),
                            IsActive = SheetValueParser.Bool(row, "IsActive", true)
                        });
                        progress.RecordSuccess();
                    }
                    catch (Exception ex)
                    {
                        progress.RecordError($"AmenityType row: {ex.Message}");
                    }
                }

                await dbContext.SaveChangesAsync(ct);
            }
        }

        private async Task ImportCrmTablesAsync(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            DataImportJob job,
            ImportProgress progress,
            CancellationToken ct)
        {
            var crmSheets = FilterSheets(sheets, CrmSheetNames);
            if (crmSheets.Count == 0)
                return;

            var delegatedRows = CountDelegatedRows(crmSheets, CrmSheetNames);
            var payload = BuildBatchPayload(job.TenantId, crmSheets);

            var (succeeded, reportedErrors) = await PostBatchAsync("CRM", payload, job, ct);
            if (!succeeded)
            {
                progress.AddErrors(reportedErrors);
                return;
            }

            progress.AddImported(delegatedRows);
            _logger.LogInformation("[DATA IMPORT]: CRM batch import delegated {Count} rows.", delegatedRows);
        }

        private async Task ImportIdentityTablesAsync(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            DataImportJob job,
            ImportProgress progress,
            CancellationToken ct)
        {
            var identitySheets = FilterSheets(sheets, IdentitySheetNames);
            if (identitySheets.Count == 0)
                return;

            var delegatedRows = CountDelegatedRows(identitySheets, IdentitySheetNames);
            var payload = BuildBatchPayload(job.TenantId, identitySheets);

            var (succeeded, reportedErrors) = await PostBatchAsync("Identity", payload, job, ct);
            if (!succeeded)
            {
                progress.AddErrors(reportedErrors);
                return;
            }

            progress.AddImported(delegatedRows);
            _logger.LogInformation("[DATA IMPORT]: Identity batch import delegated {Count} rows.", delegatedRows);
        }

        private static Dictionary<string, List<Dictionary<string, string>>> FilterSheets(
            Dictionary<string, List<Dictionary<string, string>>> sheets,
            string[] wantedNames)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in wantedNames)
            {
                if (sheets.TryGetValue(name, out var rows) && rows.Count > 0)
                    result[name] = rows;
            }

            return result;
        }

        private static int CountDelegatedRows(Dictionary<string, List<Dictionary<string, string>>> sheets, string[] wantedNames)
        {
            var total = 0;
            foreach (var name in wantedNames)
            {
                if (sheets.TryGetValue(name, out var rows))
                    total += rows.Count;
            }

            return total;
        }

        private static int CountRows(Dictionary<string, List<Dictionary<string, string>>> sheets, string sheetName)
        {
            return sheets.TryGetValue(sheetName, out var rows) ? rows.Count : 0;
        }

        private static object BuildBatchPayload(string tenantId, Dictionary<string, List<Dictionary<string, string>>> sheets)
        {
            return new
            {
                tenantId = tenantId,
                sheets = sheets
            };
        }

        private async Task<(bool succeeded, List<string> errors)> PostBatchAsync(
            string serviceName,
            object payload,
            DataImportJob job,
            CancellationToken ct)
        {
            var baseUrl = serviceName == "CRM"
                ? ResolveGatewayBaseUrl()
                : (ResolveBaseUrl("Identity:BaseUrl") ?? "http://localhost:5001");

            var gatewayPath = serviceName == "CRM"
                ? "/gateway/crm/import"
                : "/api/auth/import";

            var targetUrl = new Uri(new Uri(baseUrl.TrimEnd('/')), gatewayPath);

            using var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrWhiteSpace(job.AuthorizationHeader))
            {
                var token = job.AuthorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? job.AuthorizationHeader["Bearer ".Length..]
                    : job.AuthorizationHeader;
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await httpClient.PostAsJsonAsync(targetUrl, payload, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[DATA IMPORT]: {Service} batch import accepted.", serviceName);
                return (true, new List<string>());
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var message = $"{serviceName} batch import failed — HTTP {response.StatusCode}: {body}";
            _logger.LogError("[DATA IMPORT]: {Message}", message);
            return (false, new List<string> { message });
        }

        private string ResolveGatewayBaseUrl()
        {
            return _configuration["Gateway:BaseUrl"]
                   ?? Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
                   ?? "http://gateway:5000";
        }

        private string ResolveBaseUrl(string key)
        {
            return _configuration[key] ?? string.Empty;
        }

        private static PropertyAsset BuildPropertyAsset(Dictionary<string, string> row, string tenantId)
        {
            return new PropertyAsset
            {
                TenantId = SheetValueParser.Text(row, "TenantId", tenantId),
                CompleteAddress = SheetValueParser.Text(row, "CompleteAddress"),
                ErfSpatialSize = SheetValueParser.Text(row, "ErfSpatialSize"),
                TitleDeedType = SheetValueParser.Text(row, "TitleDeedType", "Freehold"),
                PortfolioZoning = SheetValueParser.Text(row, "PortfolioZoning"),
                MarketClass = SheetValueParser.Text(row, "MarketClass", "Sale"),
                IsForSale = SheetValueParser.Bool(row, "IsForSale", true),
                IsForRent = SheetValueParser.Bool(row, "IsForRent"),
                PropertyTypeId = SheetValueParser.Int(row, "PropertyTypeId", 1),
                ValuationPrice = SheetValueParser.Decimal(row, "ValuationPrice"),
                SalePrice = SheetValueParser.NullableDecimal(row, "SalePrice"),
                TenantInstallationAllowance = SheetValueParser.Decimal(row, "TenantInstallationAllowance"),
                Latitude = SheetValueParser.Decimal(row, "Latitude"),
                Longitude = SheetValueParser.Decimal(row, "Longitude"),
                VerificationDocumentsJson = SheetValueParser.Text(row, "VerificationDocumentsJson", "[]"),
                VerificationDocumentMetadataJson = SheetValueParser.Text(row, "VerificationDocumentMetadataJson", "[]"),
                GalleryImageJson = SheetValueParser.Text(row, "GalleryImageJson", "[]"),
                LinkedOwnerId = SheetValueParser.Text(row, "LinkedOwnerId"),
                PropertyDescription = SheetValueParser.Text(row, "PropertyDescription"),
                CreatedAt = SheetValueParser.DateTimeValue(row, "CreatedAt") ?? DateTime.UtcNow
            };
        }

        private static PropertyUnit BuildPropertyUnit(Dictionary<string, string> row, int propertyId, string tenantId)
        {
            return new PropertyUnit
            {
                PropertyId = propertyId,
                UnitNumber = SheetValueParser.Text(row, "UnitNumber"),
                UnitDescription = SheetValueParser.Text(row, "UnitDescription"),
                NetRentableAreaSqM = SheetValueParser.Decimal(row, "NetRentableAreaSqM"),
                SpecificUnitRentalPrice = SheetValueParser.NullableDecimal(row, "SpecificUnitRentalPrice"),
                SpecificUnitSalePrice = SheetValueParser.NullableDecimal(row, "SpecificUnitSalePrice"),
                RentalRate = SheetValueParser.NullableDecimal(row, "RentalRate"),
                RentEscalationPercentage = SheetValueParser.NullableDecimal(row, "RentEscalationPercentage"),
                LeaseTermMonths = SheetValueParser.NullableInt(row, "LeaseTermMonths"),
                UnitImagesJson = SheetValueParser.Text(row, "UnitImagesJson", "[]"),
                UnitDocumentsJson = SheetValueParser.Text(row, "UnitDocumentsJson", "[]"),
                IsCurrentlyVacant = SheetValueParser.Bool(row, "IsCurrentlyVacant", true),
                CreatedAt = SheetValueParser.DateTimeValue(row, "CreatedAt") ?? DateTime.UtcNow
            };
        }

        private async Task UpdateTrackingJobAsync(PropertyDbContext dbContext, string jobId, string status, ImportProgress progress, bool completed = false)
        {
            var trackingJob = await dbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (trackingJob == null) return;

            trackingJob.Status = status;
            trackingJob.ProcessedRows = progress.ProcessedRows;
            trackingJob.SuccessCount = progress.SuccessCount;
            trackingJob.ErrorCount = progress.ErrorCount;
            trackingJob.ErrorsJson = JsonSerializer.Serialize(progress.Errors.Take(100).ToList());
            if (completed)
                trackingJob.CompletedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        private static async Task CreateCompletionNotificationAsync(PropertyDbContext dbContext, DataImportJob job, ImportProgress progress)
        {
            var notification = new Notification
            {
                TenantId = job.TenantId,
                UserId = job.RequestedBy,
                Category = "import",
                Title = progress.ErrorCount == 0 ? "Data import completed" : "Data import completed with errors",
                Body = progress.ErrorCount == 0
                    ? $"{job.FileName}: all tables imported successfully."
                    : $"{job.FileName}: import finished with {progress.ErrorCount} errors.",
                LinkType = "import",
                LinkId = job.JobId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();
        }

        private sealed class ImportProgress
        {
            public int TotalRows { get; set; }
            public int ProcessedRows { get; set; }
            public int SuccessCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; } = new();
            public bool Failed { get; private set; }

            public void RecordSuccess()
            {
                SuccessCount++;
                ProcessedRows++;
            }

            public void RecordError(string message)
            {
                Errors.Add(message);
                ErrorCount++;
                ProcessedRows++;
            }

            public void AddImported(int count)
            {
                SuccessCount += count;
                ProcessedRows += count;
            }

            public void AddErrors(List<string> errors)
            {
                foreach (var error in errors)
                {
                    Errors.Add(error);
                    ErrorCount++;
                }
            }

            public string Status => Failed ? "failed" : (ErrorCount == 0 ? "completed" : "completed_with_errors");

            public void FinalizeStatus()
            {
                if (ErrorCount > 0 && SuccessCount == 0)
                    Failed = true;
            }
        }
    }
}
