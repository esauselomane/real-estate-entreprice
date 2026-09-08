using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using DataImport.Worker.Models;
using ClosedXML.Excel;

namespace DataImport.Worker.Services
{
    public class ImportWorker : BackgroundService
    {
        private readonly ILogger<ImportWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _bootstrapServers;
        private readonly IProducer<string, string> _producer;
        private const string Topic = TopicNames.DataImport;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ImportWorker(ILogger<ImportWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                AllowAutoCreateTopics = true,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "data-import-worker",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(Topic);

            _logger.LogInformation("[DATA IMPORT WORKER]: Listening on topic '{Topic}'...", Topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result == null) continue;

                    var payload = result.Message.Value;
                    _logger.LogInformation("[DATA IMPORT WORKER]: Received job {Key}", result.Message.Key);

                    await ProcessImportAsync(payload, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "[DATA IMPORT WORKER]: Kafka consume error.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DATA IMPORT WORKER]: Processing error.");
                }
            }
        }

        private async Task PublishAsync<TEvent>(string topic, string key, TEvent evt)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                var message = new Message<string, string> { Key = key, Value = json };
                var receipt = await _producer.ProduceAsync(topic, message);
                _logger.LogInformation("[KAFKA]: Published {EventType} to {Topic} [Partition: {Partition}] Offset: {Offset}",
                    typeof(TEvent).Name, receipt.Topic, receipt.Partition, receipt.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA ERROR]: Failed to publish {EventType}", typeof(TEvent).Name);
                throw;
            }
        }

        private async Task ProcessImportAsync(string payload, CancellationToken ct)
        {
            await using var scope = new ImportProcessingScope(_logger, _configuration);
            var job = await scope.DeserializeJobAsync(payload);
            if (job == null) return;

            await scope.UpdateJobStatusAsync(job.JobId, "processing");

            try
            {
                var bytes = Convert.FromBase64String(job.Base64Content);
                using var stream = new MemoryStream(bytes);
                using var workbook = new XLWorkbook(stream);

                // Read all sheets into dictionaries keyed by header
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

                var totalRows = sheets.Values.Sum(r => r.Count);
                await scope.UpdateJobCountsAsync(job.JobId, totalRows, 0, 0, 0);

                var errors = new List<string>();
                var successCount = 0;
                var propertyAddressToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                // 1. Import properties first so units can link to them
                if (sheets.TryGetValue("Properties", out var propertyRows))
                {
                    foreach (var row in propertyRows)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            await scope.ImportPropertyRowAsync(row, job.TenantId);
                            var address = row.GetValueOrDefault("Address") ?? string.Empty;
                            var prop = await scope.GetPropertyByAddressAsync(address, job.TenantId);
                            if (prop != null)
                                propertyAddressToId[address] = prop.PropertyId;
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Property row: {ex.Message}");
                        }
                        await scope.IncrementProcessedAsync(job.JobId);
                    }
                }

                // 2. Import units, linking to properties by PropertyRef (address)
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
                                await scope.ImportUnitRowAsync(row, propertyId);
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
                        await scope.IncrementProcessedAsync(job.JobId);
                    }
                }

                // 3. Import contacts — publish to Kafka for CRM to consume
                if (sheets.TryGetValue("Contacts", out var contactRows))
                {
                    foreach (var row in contactRows)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            await PublishContactEventAsync(row, job.TenantId);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Contact row: {ex.Message}");
                        }
                        await scope.IncrementProcessedAsync(job.JobId);
                    }
                }

                await scope.CompleteJobAsync(job.JobId, successCount, errors.Count, errors);

                // Notify via Kafka instead of HTTP
                var status = errors.Count == 0 ? "completed" : "completed_with_errors";
                var message = errors.Count == 0 ? "Import completed successfully." : $"Import completed with {errors.Count} errors.";
                await PublishImportCompletedEventAsync(job, status, message, successCount, errors.Count, errors);

                _logger.LogInformation("[DATA IMPORT WORKER]: Job {JobId} finished — {Success} success, {Errors} errors",
                    job.JobId, successCount, errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DATA IMPORT WORKER]: Failed to process job {JobId}", job.JobId);
                await scope.UpdateJobStatusAsync(job.JobId, "failed");
                await PublishImportCompletedEventAsync(job, "failed", $"Import failed: {ex.Message}", 0, 0, new List<string>());
            }
        }

        private async Task PublishContactEventAsync(Dictionary<string, string?> row, string tenantId)
        {
            var contactEvent = new ContactImportedEvent(
                tenantId,
                row.GetValueOrDefault("FullName") ?? string.Empty,
                row.GetValueOrDefault("Email") ?? string.Empty,
                row.GetValueOrDefault("Phone") ?? string.Empty,
                row.GetValueOrDefault("CompanyName") ?? string.Empty,
                row.GetValueOrDefault("RecordType") ?? "Owner",
                row.GetValueOrDefault("PrimaryLocation") ?? string.Empty,
                decimal.TryParse(row.GetValueOrDefault("BudgetLimit"), out var bl) ? bl : 0,
                row.GetValueOrDefault("PropertyTypeInterests") ?? string.Empty,
                row.GetValueOrDefault("AreaInterestIds") ?? string.Empty,
                row.GetValueOrDefault("Notes") ?? string.Empty
            );

            await PublishAsync(TopicNames.Contacts, Guid.NewGuid().ToString("N"), contactEvent);
        }

        private async Task PublishImportCompletedEventAsync(ImportJobMessage job, string status, string message, int successCount, int errorCount, List<string> errors)
        {
            var completedEvent = new ImportJobCompletedEvent(
                JobId: job.JobId,
                ImportType: job.ImportType,
                SuccessCount: successCount,
                ErrorCount: errorCount,
                Errors: errors,
                TenantId: job.TenantId,
                RequestedBy: job.RequestedBy,
                Status: status,
                CompletedAt: DateTime.UtcNow
            );

            await PublishAsync(TopicNames.ImportCompleted, job.JobId, completedEvent);

            // Also publish a generic notification event so CRM can alert the user
                var notificationEvent = new NotificationCreatedEvent(
                    TenantId: job.TenantId,
                    UserId: job.RequestedBy,
                    Category: "import",
                    Title: $"Data import {status}",
                    Body: $"{job.FileName}: {message}",
                    LinkType: "import",
                    LinkId: job.JobId
                );

            await PublishAsync(TopicNames.Notifications, job.JobId, notificationEvent);
        }
    }
}
