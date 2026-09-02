using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
            QuestPDF.Settings.License = LicenseType.Community;
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

        private static async Task<byte[]?> DownloadMapTileAsync(decimal lat, decimal lon, int maxRetries = 4)
        {
            var mapUrl = BuildStaticMapUrl(lat, lon);
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                var bytes = await DownloadImageBytesAsync(mapUrl);
                if (bytes != null && bytes.Length > 1024) return bytes;
                await Task.Delay(600);
            }
            return null;
        }

        private static string BuildStaticMapUrl(decimal lat, decimal lon, int width = 600, int height = 400, int zoom = 14)
        {
            return $"https://static-maps.yandex.ru/1.x/?ll={lon},{lat}&z={zoom}&l=map&size={width},{height}";
        }

        private static readonly TextStyle _titleStyle = TextStyle.Default.FontSize(24).FontColor("#0F172A").SemiBold();
        private static readonly TextStyle _metaStyle = TextStyle.Default.FontSize(10).FontColor("#94A3B8");
        private static readonly TextStyle _subTitleStyle = TextStyle.Default.FontSize(16).FontColor("#0F172A").Bold();
        private static readonly TextStyle _headerStyle = TextStyle.Default.FontSize(14).FontColor("#0F172A").Bold();
        private static readonly TextStyle _bodyStyle = TextStyle.Default.FontSize(11).FontColor("#334155");
        private static readonly TextStyle _sectionHeaderStyle = TextStyle.Default.FontSize(12).FontColor("#0F172A").Bold();
        private static readonly TextStyle _alertHeaderStyle = TextStyle.Default.FontSize(12).FontColor("#1E40AF").Bold();
        private static readonly TextStyle _alertPriceStyle = TextStyle.Default.FontSize(14).FontColor("#1E40AF").Bold();

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
                // 1. Deserialize incoming transactional context parameters
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

                var groupedByProperty = subUnits
                    .GroupBy(u => u.PropertyId)
                    .ToList();

                var logoBytes = !string.IsNullOrEmpty(companyLogoUrl) ? await DownloadImageBytesAsync(companyLogoUrl) : null;
                var mapImagesByProperty = new Dictionary<int, byte[]>();
                foreach (var group in groupedByProperty)
                {
                    var sample = group.First();
                    var mapBytes = await DownloadMapTileAsync(sample.Latitude, sample.Longitude);
                    if (mapBytes != null) mapImagesByProperty[group.Key] = mapBytes;
                }

                // 2. ORCHESTRATE ASYNC MULTI-PAGE QUESTPDF COMPILER DESK
                var compiledDocumentBytes = Document.Create(container =>
                {
                    // --- PAGE 1: BRANDED COVER PRESENTATION PROFILE ---
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor("#0F172A");

                        page.Content().Column(col =>
                        {
                            col.Spacing(20);
                            col.Item().Text("REAL ESTATE PORTFOLIO PROPOSAL").Style(_titleStyle);

                            // Company logo with fallback placeholder
                            col.Item().Column(logoCol =>
                            {
                                logoCol.Spacing(12);
                                if (logoBytes != null)
                                {
                                    logoCol.Item().Height(60).Width(60).Image(logoBytes);
                                }
                                else
                                {
                                    logoCol.Item().Height(60).Width(60).Background("#1E293B").Border(1).BorderColor("#334155").AlignCenter().Column(inner =>
                                    {
                                        inner.Item().AlignCenter().Text(companyName.Split(' ').Select(w => w[0]).Take(3).Aggregate("", (a, b) => a + b)).Style(_headerStyle);
                                    });
                                }
                                logoCol.Item().Text(companyName).Style(_subTitleStyle);
                            });

                            col.Item().PaddingTop(60).BorderTop(1).BorderColor("#334155").Column(bCol =>
                            {
                                bCol.Spacing(6);
                                bCol.Item().Text("PREPARED BY:").Style(_metaStyle);
                                bCol.Item().Text(brokerName).Style(_subTitleStyle);
                                bCol.Item().Text(brokerEmail).Style(_metaStyle);
                                bCol.Item().Text(brokerPhone).Style(_metaStyle);
                            });

                            col.Item().PaddingTop(40).BorderTop(1).BorderColor("#334155").Column(bCol =>
                            {
                                bCol.Spacing(6);
                                bCol.Item().Text("PREPARED FOR:").Style(_metaStyle);
                                bCol.Item().Text(clientName).Style(_subTitleStyle);
                                if (!string.IsNullOrEmpty(clientEmail)) bCol.Item().Text(clientEmail).Style(_metaStyle);
                                if (!string.IsNullOrEmpty(clientPhone)) bCol.Item().Text(clientPhone).Style(_metaStyle);
                            });
                        });
                    });

                    // --- PAGE 2: LOCATION & MAPPING ---
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor("#F8FAFC");

                        page.Header().PaddingBottom(10).Text("LOCATION & MAPPING").Style(_headerStyle);

                        page.Content().Column(col =>
                        {
                            col.Spacing(15);
                            col.Item().Text("Properties included in this proposal:").Style(_bodyStyle);

                            foreach (var group in groupedByProperty)
                            {
                                var sampleUnit = group.First();
                                col.Item().Padding(10).Background("#EFF6FF").Border(1).BorderColor("#BFDBFE").Column(propCol =>
                                {
                                    propCol.Spacing(4);
                                    propCol.Item().Text($"Property {group.Key}").Style(_sectionHeaderStyle);
                                    if (!string.IsNullOrEmpty(sampleUnit.PropertyDescription))
                                    {
                                        propCol.Item().Text(sampleUnit.PropertyDescription).Style(_bodyStyle);
                                    }
                                    propCol.Item().Text($"Coordinates: {sampleUnit.Latitude:F6}, {sampleUnit.Longitude:F6}").Style(_metaStyle);
                                });
                            }

                            col.Item().PaddingTop(20).Column(mapCol =>
                            {
                                mapCol.Spacing(10);
                                foreach (var group in groupedByProperty)
                                {
                                    var sampleUnit = group.First();
                                    mapCol.Item().Text($"Property {group.Key} — Lat {sampleUnit.Latitude:F6}, Lon {sampleUnit.Longitude:F6}").Style(_sectionHeaderStyle);
                                    if (mapImagesByProperty.TryGetValue(group.Key, out var mapBytes))
                                    {
                                        mapCol.Item().Height(220).Image(mapBytes);
                                    }
                                    else
                                    {
                                        mapCol.Item().Height(220).Background("#CBD5E1").Border(1).BorderColor("#94A3B8").AlignCenter().Text($"[Map tile for Property {group.Key} could not be loaded]").Style(_metaStyle);
                                    }
                                }
                            });
                        });
                    });

                    // --- PAGE 3+: UNIT-LEVEL BREAKDOWN BY PROPERTY ---
                    foreach (var group in groupedByProperty)
                    {
                        var propertyUnits = group.ToList();
                        var sampleUnit = propertyUnits.First();

                        foreach (var unit in propertyUnits)
                        {
                            container.Page(page =>
                            {
                                page.Size(PageSizes.A4);
                                page.Margin(1.5f, Unit.Centimetre);
                                page.PageColor("#F8FAFC");

                                page.Header().PaddingBottom(10).Text($"UNIT: {unit.UnitNumber}").Style(_headerStyle);

                                page.Content().Column(col =>
                                {
                                    col.Spacing(12);
                                    col.Item().Text($"Unit Description: {unit.UnitDescription}").Style(_bodyStyle);
                                    col.Item().Text($"Net Rentable Area: {unit.NetRentableAreaSqM:F2} m²").Style(_bodyStyle);

                                    if (marketClass.Equals("Rental", StringComparison.OrdinalIgnoreCase))
                                    {
                                        col.Item().PaddingTop(20).Text("RENTAL TERMS").Style(_sectionHeaderStyle);
                                        col.Item().Text($"Monthly Rental Rate: R {unit.RentalRate?.ToString("N2") ?? "0.00"}").Style(_bodyStyle);
                                        col.Item().Text($"Annual Escalation: {unit.RentEscalationPercentage?.ToString("F2") ?? "0.00"}%").Style(_bodyStyle);
                                        col.Item().Text($"Lease Term: {unit.LeaseTermMonths?.ToString() ?? "12"} months").Style(_bodyStyle);
                                    }
                                    else
                                    {
                                        col.Item().PaddingTop(20).Text("SALE DETAILS").Style(_sectionHeaderStyle);
                                        col.Item().Text($"Purchase Price: R {unit.SalePrice?.ToString("N2") ?? "0.00"} (Excl VAT)").Style(_headerStyle);
                                        if (unit.SalePrice.HasValue)
                                        {
                                            var bondEstimate = unit.SalePrice.Value * 0.8m * 0.11m / 12;
                                            col.Item().Text($"Estimated Bond Repayment (80% over 20 yrs): R {bondEstimate.ToString("N2")} /month").Style(_bodyStyle);
                                        }
                                    }

                                    if (unit.SelectedImageRefs.Any())
                                    {
                                        col.Item().PaddingTop(20).Text("UNIT IMAGES").Style(_sectionHeaderStyle);
                                        col.Item().Grid(grid =>
                                        {
                                            grid.Spacing(10);
                                            grid.Columns(3);
                                            foreach (var img in unit.SelectedImageRefs)
                                            {
                                                grid.Item().Height(70).Background("#E2E8F0").Border(1).BorderColor("#CBD5E1").Padding(5).AlignCenter().Text(img).Style(_bodyStyle);
                                            }
                                        });
                                    }
                                });
                            });
                        }
                    }
                }).GeneratePdf();

                 // 3. PERSIST THE COMPILED BINARY STORAGE ARTIFACT RECORD ONTO DISK VOLUMES
                 // The server "brochure" drop folder is relative to the worker process root.
                 string destinationDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GeneratedBrochures"));
                 if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

                 string finalFilePath = Path.Combine(destinationDirectory, $"Brochure_Deal_{dealId}.pdf");
                 await File.WriteAllBytesAsync(finalFilePath, compiledDocumentBytes);

                 _logger.LogInformation($"[COMPILATION SUCCESSFUL]: PDF Brochure payload saved to target node disk: '{finalFilePath}'");

                 // 4. CALLBACK THE CRM to soft-delete the brochure card and advance the deal
                 //    to the next pipeline stage (Presentation -> ProposalQuoteSent). Best-effort:
                 //    a gateway failure must never roll back an already-produced document.
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
