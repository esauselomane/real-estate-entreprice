using ClosedXML.Excel;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.BackgroundJobs
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
                    var dbContext = scope.ServiceProvider.GetRequiredService<CRMDbContext>();

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

        private async Task ProcessImportJobAsync(DataImportJob job, CRMDbContext dbContext, CancellationToken ct)
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

            var successCount = 0;
            var errors = new List<string>();

            // Import contacts - create in CRM
            if (sheets.TryGetValue("Contacts", out var contactRows))
            {
                foreach (var row in contactRows)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var contact = new Contact
                        {
                            TenantId = job.TenantId,
                            FullName = row.GetValueOrDefault("FullName") ?? string.Empty,
                            EmailAddress = row.GetValueOrDefault("Email") ?? string.Empty,
                            PhoneNumber = row.GetValueOrDefault("Phone") ?? string.Empty,
                            CompanyName = row.GetValueOrDefault("CompanyName") ?? string.Empty,
                            ContactType = Enum.Parse<ContactType>(row.GetValueOrDefault("RecordType") ?? "Owner"),
                            PrimaryLocation = row.GetValueOrDefault("PrimaryLocation") ?? string.Empty,
                            BudgetLimit = decimal.TryParse(row.GetValueOrDefault("BudgetLimit"), out var bl) ? bl : 0,
                            PropertyTypeInterests = row.GetValueOrDefault("PropertyTypeInterests") ?? string.Empty,
                            AreaInterestIds = row.GetValueOrDefault("AreaInterestIds") ?? string.Empty,
                            Notes = row.GetValueOrDefault("Notes") ?? string.Empty,
                            CreatedAt = DateTime.UtcNow
                        };
                        dbContext.Contacts.Add(contact);
                        await dbContext.SaveChangesAsync(ct);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Contact row: {ex.Message}");
                    }
                }
            }

            // Create notification for import completion
            var notification = new Notification
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