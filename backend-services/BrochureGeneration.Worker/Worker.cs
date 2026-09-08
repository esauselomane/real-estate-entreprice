using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using System.Text;
using System.Text.Json;

namespace BrochureGeneration.Worker
{
    internal record BrochureSubUnit
    {
        public int PropertyId { get; set; }
        public string PropertyDescription { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitDescription { get; set; } = string.Empty;
        public decimal NetRentableAreaSqM { get; set; }
        public decimal? RentalRate { get; set; }
        public decimal? RentEscalationPercentage { get; set; }
        public int? LeaseTermMonths { get; set; }
        public decimal? SalePrice { get; set; }
        public List<string> SelectedImageRefs { get; set; } = new();
    }

    public class BrochureKafkaConsumerWorker : BackgroundService
    {
        private readonly ILogger<BrochureKafkaConsumerWorker> _logger;
        private readonly ConsumerConfig _consumerConfig;
        private readonly IProducer<string, string> _producer;
        private readonly string _bootstrapServers;
        private const string TopicName = TopicNames.BrochureGenerationRequests;
        private const string CompletedTopic = TopicNames.BrochureGenerationCompleted;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public BrochureKafkaConsumerWorker(ILogger<BrochureKafkaConsumerWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "brochure-generator-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                AllowAutoCreateTopics = true,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        private static async Task<byte[]?> DownloadImageBytesAsync(string url, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            using var http = new HttpClient();
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await http.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content.ReadAsByteArrayAsync();
                }
                catch
                {
                    if (attempt == maxRetries) return null;
                    await Task.Delay(500);
                }
            }
            return null;
        }

        private static string ToBase64(byte[] bytes) => Convert.ToBase64String(bytes);

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string BuildBrochureHtml(
            string brokerName, string brokerEmail, string brokerPhone,
            string companyName, string logoDataUrl,
            string clientName, string clientEmail, string clientPhone,
            string marketClass, decimal tenantAllowance,
            List<BrochureSubUnit> subUnits)
        {
            var logoHtml = string.IsNullOrEmpty(logoDataUrl)
                ? $"<div style=\"width:60px;height:60px;background:#1E293B;border:1px solid #334155;border-radius:8px;display:flex;align-items:center;justify-content:center;color:#0F172A;font-weight:bold;font-size:14px;\">{EscapeHtml(companyName.Split(' ').Select(w => w.Length > 0 ? w[0].ToString() : string.Empty).Take(3).Aggregate("", (a, b) => a + b))}</div>"
                : $"<img src=\"{logoDataUrl}\" style=\"width:60px;height:60px;object-fit:contain;border-radius:8px;border:1px solid #334155;\" />";

            var coverHtml = $@"
            <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #0F172A; page-break-after: always;"">
              <div style=""padding: 40px;"">
                <h1 style=""font-size: 28px; font-weight: 600; margin: 0 0 20px 0;"">REAL ESTATE PORTFOLIO PROPOSAL</h1>
                <div style=""display: flex; align-items: center; gap: 12px; margin-bottom: 60px;"">
                  {logoHtml}
                  <span style=""font-size: 18px; font-weight: bold;"">{EscapeHtml(companyName)}</span>
                </div>
                <div style=""border-top: 1px solid #334155; padding-top: 20px; margin-bottom: 30px;"">
                  <p style=""font-size: 11px; color: #94A3B8; margin: 0 0 4px 0;"">PREPARED BY:</p>
                  <p style=""font-size: 16px; font-weight: bold; margin: 0;"">{EscapeHtml(brokerName)}</p>
                  <p style=""font-size: 11px; color: #94A3B8; margin: 4px 0 0 0;"">{EscapeHtml(brokerEmail)}</p>
                  <p style=""font-size: 11px; color: #94A3B8; margin: 4px 0 0 0;"">{EscapeHtml(brokerPhone)}</p>
                </div>
                <div style=""border-top: 1px solid #334155; padding-top: 20px;"">
                  <p style=""font-size: 11px; color: #94A3B8; margin: 0 0 4px 0;"">PREPARED FOR:</p>
                  <p style=""font-size: 16px; font-weight: bold; margin: 0;"">{EscapeHtml(clientName)}</p>
                  {(string.IsNullOrEmpty(clientEmail) ? "" : $"<p style=\"font-size: 11px; color: #94A3B8; margin: 4px 0 0 0;\">{EscapeHtml(clientEmail)}</p>")}
                  {(string.IsNullOrEmpty(clientPhone) ? "" : $"<p style=\"font-size: 11px; color: #94A3B8; margin: 4px 0 0 0;\">{EscapeHtml(clientPhone)}</p>")}
                </div>
              </div>
            </div>";

            var mapPageHtml = $@"
            <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #0F172A; page-break-after: always; padding: 24px;"">
              <h2 style=""font-size: 18px; font-weight: bold; margin: 0 0 20px 0; padding-bottom: 10px; border-bottom: 1px solid #E2E8F0;"">LOCATION & MAPPING</h2>
              <p style=""font-size: 12px; color: #334155; margin: 0 0 16px 0;"">All units included in this proposal:</p>
              <div id=""map"" style=""width: 100%; height: 500px; border: 1px solid #94A3B8; border-radius: 6px;""></div>
              <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
              <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
              <script>
                var map = L.map('map').setView([0, 0], 2);
                L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                  attribution: '&copy; OpenStreetMap contributors',
                  maxZoom: 19
                }}).addTo(map);
                var markers = [];
                {string.Join("", subUnits.Select((u, i) => $"markers.push(L.marker([{u.Latitude}, {u.Longitude}]).addTo(map).bindPopup('<b>{EscapeHtml(u.UnitNumber)}</b><br>{EscapeHtml(u.UnitDescription)}'));"))}
                if (markers.length > 0) {{
                  var group = new L.featureGroup(markers);
                  map.addLayer(group);
                  map.fitBounds(group.getBounds().pad(0.2));
                  map.setZoom(map.getZoom() - 1);
                }}
                setTimeout(function() {{ window.brochureReady = true; }}, 1000);
              </script>
            </div>";

            var unitPagesHtml = subUnits.Select(unit =>
            {
                var rentalHtml = marketClass.Equals("Rental", StringComparison.OrdinalIgnoreCase) ? $@"
                <div style=""margin-top: 20px;"">
                  <h3 style=""font-size: 13px; font-weight: bold; margin: 0 0 8px 0;"">RENTAL TERMS</h3>
                  <p style=""font-size: 12px; color: #334155; margin: 0 0 4px 0;"">Monthly Rental Rate: R {unit.RentalRate?.ToString("N2") ?? "0.00"}</p>
                  <p style=""font-size: 12px; color: #334155; margin: 0 0 4px 0;"">Annual Escalation: {unit.RentEscalationPercentage?.ToString("F2") ?? "0.00"}%</p>
                  <p style=""font-size: 12px; color: #334155; margin: 0;"">Lease Term: {unit.LeaseTermMonths?.ToString() ?? "12"} months</p>
                </div>" : $@"
                <div style=""margin-top: 20px;"">
                  <h3 style=""font-size: 13px; font-weight: bold; margin: 0 0 8px 0;"">SALE DETAILS</h3>
                  <p style=""font-size: 14px; font-weight: bold; color: #0F172A; margin: 0 0 4px 0;"">Purchase Price: R {unit.SalePrice?.ToString("N2") ?? "0.00"} (Excl VAT)</p>
                  {(unit.SalePrice.HasValue ? $"<p style=\"font-size: 12px; color: #334155; margin: 0;\">Estimated Bond Repayment (80% over 20 yrs): R {(unit.SalePrice.Value * 0.8m * 0.11m / 12).ToString("N2")} /month</p>" : "")}
                </div>";

                var imagesHtml = unit.SelectedImageRefs.Any() ? $@"
                <div style=""margin-top: 20px;"">
                  <h3 style=""font-size: 13px; font-weight: bold; margin: 0 0 10px 0;"">UNIT IMAGES</h3>
                  <div style=""display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px;"">
                    {string.Join("", unit.SelectedImageRefs.Select((img, idx) => $"<img src=\"{EscapeHtml(img)}\" style=\"height: 70px; width: 100%; object-fit: cover; border: 1px solid #CBD5E1; border-radius: 4px; display: block;\" />"))}
                  </div>
                </div>" : "";

                return $@"
                <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #0F172A; page-break-after: always; padding: 24px;"">
                  <h2 style=""font-size: 18px; font-weight: bold; margin: 0 0 16px 0; padding-bottom: 10px; border-bottom: 1px solid #E2E8F0;"">UNIT: {EscapeHtml(unit.UnitNumber)}</h2>
                  <p style=""font-size: 12px; color: #334155; margin: 0 0 6px 0;""><strong>Unit Description:</strong> {EscapeHtml(unit.UnitDescription)}</p>
                  <p style=""font-size: 12px; color: #334155; margin: 0 0 16px 0;""><strong>Net Rentable Area:</strong> {unit.NetRentableAreaSqM:F2} m²</p>
                  {rentalHtml}
                  {imagesHtml}
                </div>";
            });

            return $@"<!DOCTYPE html>
            <html>
            <head>
              <meta charset=""utf-8"">
              <style>
                @page {{ size: A4; margin: 0; }}
                body {{ margin: 0; padding: 0; }}
                img {{ max-width: 100%; height: auto; }}
              </style>
            </head>
            <body>
              {coverHtml}
              {mapPageHtml}
              {string.Join("", unitPagesHtml)}
            </body>
            </html>";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();
            consumer.Subscribe(TopicName);
            _logger.LogInformation($"[WORKER LOG]: Active broker consumer mapped to channel: '{TopicName}'");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult == null) continue;

                    _logger.LogInformation($"[EVENT DEQUEUED]: Preparing document batch token key: {consumeResult.Message.Key}");

                    bool taskSucceeded = await ProcessGenerationEventAsync(consumeResult.Message.Value);

                    if (taskSucceeded)
                    {
                        consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError($"[WORKER LOOP FAULT]: Exception encountered in execution pool: {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }
            consumer.Close();
        }

        private async Task<bool> ProcessGenerationEventAsync(string messageJson)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<BrochureGenerationRequestedEvent>(messageJson, _jsonOptions);
                if (evt == null)
                {
                    _logger.LogWarning("[BROCHURE]: Failed to deserialize event payload");
                    return false;
                }

                _logger.LogInformation("[BROCHURE]: Processing DealId={DealId} Broker={Broker} Client={Client} Units={Units}",
                    evt.DealId, evt.BrokerName, evt.ClientName, evt.SubUnits.Count);

                await new BrowserFetcher().DownloadAsync();
                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });

                var subUnits = evt.SubUnits.Select(u => new BrochureSubUnit
                {
                    PropertyId = u.PropertyId,
                    PropertyDescription = u.PropertyDescription,
                    Latitude = u.Latitude,
                    Longitude = u.Longitude,
                    UnitNumber = u.UnitNumber,
                    UnitDescription = u.UnitDescription,
                    NetRentableAreaSqM = u.NetRentableAreaSqM,
                    RentalRate = u.RentalRate,
                    RentEscalationPercentage = u.RentEscalationPercentage,
                    LeaseTermMonths = u.LeaseTermMonths,
                    SalePrice = u.SalePrice,
                    SelectedImageRefs = u.SelectedImageRefs
                }).ToList();

                var logoBytes = !string.IsNullOrEmpty(evt.CompanyLogoUrl) ? await DownloadImageBytesAsync(evt.CompanyLogoUrl) : null;
                var logoDataUrl = logoBytes != null ? $"data:image/png;base64,{ToBase64(logoBytes)}" : string.Empty;

                var html = BuildBrochureHtml(
                    evt.BrokerName, evt.BrokerEmail, evt.BrokerPhone,
                    evt.CompanyName, logoDataUrl,
                    evt.ClientName, evt.ClientEmail, evt.ClientPhone,
                    evt.MarketClass, evt.TenantInstallationAllowance,
                    subUnits);

                byte[] compiledDocumentBytes;
                using (var page = await browser.NewPageAsync())
                {
                    await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
                    await page.SetContentAsync(html);
                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 120000 });
                    await page.EvaluateExpressionAsync("document.fonts.ready");
                    await page.EvaluateExpressionAsync("window.brochureReady || true");
                    compiledDocumentBytes = await page.PdfDataAsync(new PdfOptions
                    {
                        Format = PuppeteerSharp.Media.PaperFormat.A4,
                        PrintBackground = true
                    });
                }

                string destinationDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GeneratedBrochures"));
                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

                string finalFilePath = Path.Combine(destinationDirectory, $"Brochure_Deal_{evt.DealId}.pdf");
                await File.WriteAllBytesAsync(finalFilePath, compiledDocumentBytes);

                _logger.LogInformation($"[COMPILATION SUCCESSFUL]: PDF Brochure payload saved to target node disk: '{finalFilePath}'");

                // Publish completion event via Kafka instead of HTTP callback
                var completedEvent = new BrochureGenerationCompletedEvent(
                    DealId: evt.DealId,
                    BrochureCardId: evt.BrochureCardId,
                    PropertyId: evt.PropertyId,
                    TenantId: evt.TenantId,
                    RequestedBy: evt.RequestedBy,
                    Success: true,
                    PdfUrl: finalFilePath,
                    ErrorMessage: null
                );

                await PublishCompletionEventAsync(completedEvent);

                // Also publish a notification event
                var notificationEvent = new NotificationCreatedEvent(
                    TenantId: evt.TenantId,
                    UserId: evt.RequestedBy,
                    Category: "brochure",
                    Title: "Brochure generation completed",
                    Body: $"Brochure for deal {evt.DealId} has been generated successfully.",
                    LinkType: "brochure",
                    LinkId: evt.BrochureCardId.ToString()
                );

                await PublishNotificationEventAsync(notificationEvent);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PDF RENDER ERROR]: Task compilation crashed inside generator core: {ex.Message}");
                return false;
            }
        }

        private async Task PublishCompletionEventAsync(BrochureGenerationCompletedEvent evt)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                var message = new Message<string, string>
                {
                    Key = evt.BrochureCardId.ToString(),
                    Value = json
                };

                var result = await _producer.ProduceAsync(CompletedTopic, message);
                _logger.LogInformation("[KAFKA]: Published BrochureGenerationCompletedEvent to {Topic} [Partition: {Partition}] Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA ERROR]: Failed to publish BrochureGenerationCompletedEvent");
            }
        }

        private async Task PublishNotificationEventAsync(NotificationCreatedEvent evt)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                var message = new Message<string, string>
                {
                    Key = evt.LinkId,
                    Value = json
                };

                var result = await _producer.ProduceAsync(TopicNames.Notifications, message);
                _logger.LogInformation("[KAFKA]: Published NotificationCreatedEvent to {Topic} [Partition: {Partition}] Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA ERROR]: Failed to publish NotificationCreatedEvent");
            }
        }

        public override void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
            base.Dispose();
        }
    }
}
