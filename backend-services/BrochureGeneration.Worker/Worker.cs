using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly string _gatewayUrl;
        private const string TopicName = "brochure-generation-requests";

        private static readonly HttpClient _http = new HttpClient();

        public BrochureKafkaConsumerWorker(ILogger<BrochureKafkaConsumerWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _gatewayUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000/gateway";
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "brochure-generator-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };
        }

        private static async Task<byte[]?> DownloadImageBytesAsync(string url, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await _http.GetAsync(url);
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
                await new BrowserFetcher().DownloadAsync();
                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });

                using var jsonDoc = JsonDocument.Parse(messageJson);
                var root = jsonDoc.RootElement;

                int dealId = root.GetProperty("DealId").GetInt32();
                string brokerName = root.GetProperty("BrokerName").GetString() ?? "Unassigned broker";
                string brokerEmail = root.GetProperty("BrokerEmail").GetString() ?? "s.jenkins@apexluxury.co.za";
                string brokerPhone = root.GetProperty("BrokerPhone").GetString() ?? "+27118845000";
                string companyName = root.GetProperty("CompanyName").GetString() ?? "Agency";
                string companyLogoUrl = root.GetProperty("CompanyLogoUrl").GetString() ?? string.Empty;
                string clientName = root.GetProperty("ClientName").GetString() ?? "Valued Client";
                string clientEmail = root.GetProperty("ClientEmail").GetString() ?? string.Empty;
                string clientPhone = root.GetProperty("ClientPhone").GetString() ?? string.Empty;
                string marketClass = root.GetProperty("MarketClass").GetString() ?? "Rental";
                decimal tenantAllowance = root.GetProperty("TenantInstallationAllowance").GetDecimal();
                int brochureCardId = root.TryGetProperty("BrochureCardId", out var bcProp) ? bcProp.GetInt32() : 0;

                var subUnits = root.GetProperty("SubUnits").EnumerateArray()
                    .Select(u => new BrochureSubUnit
                    {
                        PropertyId = u.GetProperty("PropertyId").GetInt32(),
                        PropertyDescription = u.GetProperty("PropertyDescription").GetString() ?? string.Empty,
                        Latitude = u.TryGetProperty("Latitude", out var latEl) && latEl.ValueKind != JsonValueKind.Null ? latEl.GetDecimal() : 0,
                        Longitude = u.TryGetProperty("Longitude", out var lonEl) && lonEl.ValueKind != JsonValueKind.Null ? lonEl.GetDecimal() : 0,
                        UnitNumber = u.GetProperty("UnitNumber").GetString() ?? "Suite",
                        UnitDescription = u.GetProperty("UnitDescription").GetString() ?? string.Empty,
                        NetRentableAreaSqM = u.GetProperty("NetRentableAreaSqM").GetDecimal(),
                        RentalRate = u.TryGetProperty("RentalRate", out var rEl) && rEl.ValueKind != JsonValueKind.Null ? rEl.GetDecimal() : (decimal?)null,
                        RentEscalationPercentage = u.TryGetProperty("RentEscalationPercentage", out var eEl) && eEl.ValueKind != JsonValueKind.Null ? eEl.GetDecimal() : (decimal?)null,
                        LeaseTermMonths = u.TryGetProperty("LeaseTermMonths", out var tEl) && tEl.ValueKind != JsonValueKind.Null ? tEl.GetInt32() : (int?)null,
                        SalePrice = u.TryGetProperty("SalePrice", out var sEl) && sEl.ValueKind != JsonValueKind.Null ? sEl.GetDecimal() : (decimal?)null,
                        SelectedImageRefs = u.TryGetProperty("SelectedImageRefs", out var iEl) && iEl.ValueKind == JsonValueKind.Array
                            ? iEl.EnumerateArray().Select(img => img.GetString() ?? string.Empty).Where(str => !string.IsNullOrEmpty(str)).Take(5).ToList()
                            : new List<string>()
                    })
                    .ToList();

                _logger.LogInformation($"[BROCHURE PAYLOAD] DealId={dealId} BrokerName={brokerName} ClientName={clientName} Units={subUnits.Count}");

                var logoBytes = !string.IsNullOrEmpty(companyLogoUrl) ? await DownloadImageBytesAsync(companyLogoUrl) : null;
                var logoDataUrl = logoBytes != null ? $"data:image/png;base64,{ToBase64(logoBytes)}" : string.Empty;

                var html = BuildBrochureHtml(brokerName, brokerEmail, brokerPhone, companyName, logoDataUrl, clientName, clientEmail, clientPhone, marketClass, tenantAllowance, subUnits);

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

                string finalFilePath = Path.Combine(destinationDirectory, $"Brochure_Deal_{dealId}.pdf");
                await File.WriteAllBytesAsync(finalFilePath, compiledDocumentBytes);

                _logger.LogInformation($"[COMPILATION SUCCESSFUL]: PDF Brochure payload saved to target node disk: '{finalFilePath}'");

                if (brochureCardId > 0)
                {
                    var callbackPayload = new
                    {
                        DealId = dealId,
                        CardId = brochureCardId,
                        FilePath = finalFilePath,
                        GeneratedAt = DateTime.UtcNow
                    };
                    try
                    {
                        var httpContent = new StringContent(JsonSerializer.Serialize(callbackPayload), Encoding.UTF8, "application/json");
                        var callbackUri = $"{_gatewayUrl}/crm/brochure-cards/{brochureCardId}/generated";
                        var callbackResponse = await _http.PostAsync(callbackUri, httpContent);
                        _logger.LogInformation($"[CALLBACK OK]: CRM notified of brochure completion for card {brochureCardId}. HTTP {(int)callbackResponse.StatusCode}");
                    }
                    catch (Exception cbEx)
                    {
                        _logger.LogWarning($"[CALLBACK SKIPPED]: Brochure PDF saved but CRM callback failed for card {brochureCardId}. Reason: {cbEx.Message}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PDF RENDER ERROR]: Task compilation crashed inside generator core: {ex.Message}");
                return false;
            }
        }
    }
}
