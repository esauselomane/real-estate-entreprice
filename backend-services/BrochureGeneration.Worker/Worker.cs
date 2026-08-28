using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BrochureGeneration.Worker
{
    public class BrochureKafkaConsumerWorker : BackgroundService
    {
        private readonly ILogger<BrochureKafkaConsumerWorker> _logger;
        private readonly ConsumerConfig _consumerConfig;
        private const string TopicName = "brochure-generation-requests";

        // Centralized Strong-Typed Typography Style Sheet Matrix to stop compilation breaks
        private readonly TextStyle _titleStyle = TextStyle.Default.FontFamily("Arial").FontSize(24).Bold().Color("#FFFFFF");
        private readonly TextStyle _subTitleStyle = TextStyle.Default.FontFamily("Arial").FontSize(16).Bold().Color("#38BDF8");
        private readonly TextStyle _headerStyle = TextStyle.Default.FontFamily("Arial").FontSize(14).Bold().Color("#0F172A");
        private readonly TextStyle _sectionHeaderStyle = TextStyle.Default.FontFamily("Arial").FontSize(11).Bold().Color("#475569");
        private readonly TextStyle _bodyStyle = TextStyle.Default.FontFamily("Arial").FontSize(10).Color("#334155");
        private readonly TextStyle _metaStyle = TextStyle.Default.FontFamily("Arial").FontSize(9).Italic().Color("#94A3B8");
        private readonly TextStyle _alertHeaderStyle = TextStyle.Default.FontFamily("Arial").FontSize(10).Bold().Color("#1E40AF");
        private readonly TextStyle _alertPriceStyle = TextStyle.Default.FontFamily("Arial").FontSize(16).Bold().Color("#1E3A8A");

        public BrochureKafkaConsumerWorker(ILogger<BrochureKafkaConsumerWorker> logger)
        {
            _logger = logger;
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "brochure-generator-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };
            QuestPDF.Settings.License = LicenseType.Community;
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
                // 1. Deserialize incoming transactional context parameters
                using var jsonDoc = JsonDocument.Parse(messageJson);
                var root = jsonDoc.RootElement;

                int dealId = root.GetProperty("DealId").GetInt32();
                string brokerName = root.GetProperty("BrokerName").GetString() ?? "Unassigned broker";
                string brokerEmail = root.GetProperty("BrokerEmail").GetString() ?? "s.jenkins@apexluxury.co.za";
                string brokerPhone = root.GetProperty("BrokerPhone").GetString() ?? "+27118845000";
                string companyRef = root.GetProperty("CompanyReferenceNumber").GetString() ?? "PROP-REF-991";
                string marketClass = root.GetProperty("MarketClass").GetString() ?? "Rental"; 
                decimal tenantAllowance = root.GetProperty("TenantInstallationAllowance").GetDecimal();
                string addressStr = root.GetProperty("CompleteAddress").GetString() ?? "Unknown Address Link";
                decimal latCoord = root.GetProperty("Latitude").GetDecimal();
                decimal lngCoord = root.GetProperty("Longitude").GetDecimal();

                var selectedImages = root.GetProperty("SelectedImages").EnumerateArray()
                    .Select(img => img.GetString() ?? string.Empty)
                    .Where(str => !string.IsNullOrEmpty(str))
                    .Take(5)
                    .ToList();

                var subUnits = root.GetProperty("SubUnits").EnumerateArray().ToList();

                // 2. ORCHESTRATE ASYNC MULTI-PAGE QUESTPDF COMPILER DESK
                var compiledDocumentBytes = Document.Create(container =>
                {
                    // --- PAGE 1: BRANDED COVER PRESENTATION PROFILE ---
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.Background("#0F172A"); 

                        page.Content().Column(col =>
                        {
                            col.Spacing(20);
                            col.Item().Text("REAL ESTATE PORTFOLIO PROPOSAL").Style(_titleStyle);
                            col.Item().Text($"Ecosystem System Broker Index: {companyRef}").Style(_metaStyle);

                            col.Item().PaddingTop(100).BorderTop(1).BorderColor("#334155").Column(bCol =>
                            {
                                bCol.Spacing(6);
                                bCol.Item().Text("PREPARED BY REGISTERED OPERATIONAL HANDLER:").Style(_metaStyle);
                                bCol.Item().Text(brokerName).Style(_subTitleStyle);
                                bCol.Item().Text($"Direct Email Line: {brokerEmail}").Style(_metaStyle);
                                bCol.Item().Text($"Direct Contact Str: {brokerPhone}").Style(_metaStyle);
                            });
                        });
                    });

                    // --- PAGE 2: TRANSIT GRID GEOSPATIAL MAP RECEPTACLE SHEET ---
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.Background("#F8FAFC");

                        page.Header().PaddingBottom(10).Text("GEOSPATIAL SITUATIONAL SITING MAP").Style(_headerStyle);
                        
                        page.Content().Column(col =>
                        {
                            col.Spacing(15);
                            col.Item().Text($"Master Asset Location Target: {addressStr}").Style(_bodyStyle);
                            
                            col.Item().Height(300).Background("#CBD5E1").Border(1).BorderColor("#94A3B8").Column(mapCol =>
                            {
                                // Fix: Alignment modifiers placed securely directly on the structural grid items
                                mapCol.Item().Padding(10).AlignCenter().Text("[Interactive Multi-Pin Placement Render Engine Canvas Area]").Style(_sectionHeaderStyle);
                                mapCol.Item().PaddingTop(100).AlignCenter().Text($"📌 Active Pin Plot Point Array Context Path: Lat {latCoord} / Lng {lngCoord}").Style(_headerStyle);
                            });
                            col.Item().Text("* Brochures grouped with multiple selection entries will render localized markers across this placement box area.").Style(_metaStyle);
                        });
                    });
                    // --- PAGE 3: MASTER ASSET FINANCIAL CONDITIONS INCENTIVES ---
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.Background("#FFFFFF");

                        page.Header().PaddingBottom(10).Text("FINANCIAL MATRIX & STRUCTURAL SPECIFICATIONS").Style(_headerStyle);

                        page.Content().Column(col =>
                        {
                            col.Spacing(15);
                            col.Item().Text($"Asset Classification Context: FOR {marketClass.ToUpper()}").Style(_subTitleStyle);

                            // Renders New Numerical Tenant Installation Fit-Out Incentive Box
                            col.Item().Background("#EFF6FF").Border(1).BorderColor("#BFDBFE").Padding(12).Column(tiCol =>
                            {
                                tiCol.Item().Text("TENANT INSTALLATION (TI) ALLOWANCE INCENTIVE").Style(_alertHeaderStyle);
                                tiCol.Item().Text($"R {tenantAllowance.ToString("N2")} Total Incentive Budget Allocation").Style(_alertPriceStyle);
                            });

                            // --- 🔀 CONDITIONAL LAYOUT FINANCIAL DATA FORKING PIPELINE ---
                            if (marketClass.Equals("Rental", StringComparison.OrdinalIgnoreCase))
                            {
                                decimal rentRate = root.TryGetProperty("BaseRentalRate", out var rProp) ? rProp.GetDecimal() : 0;
                                decimal escalation = root.TryGetProperty("RentEscalationPercentage", out var eProp) ? eProp.GetDecimal() : 0;
                                int terms = root.TryGetProperty("LeaseTermMonths", out var tProp) ? tProp.GetInt32() : 12;

                                col.Item().Text($"Baseline Monthly Rental: R {rentRate.ToString("N2")} /month").Style(_bodyStyle);
                                col.Item().Text($"Annual Compound Escalation Rate: {escalation.ToString("F2")}% per Annum").Style(_bodyStyle);
                                col.Item().Text($"Mandated Initial Lease Commitment Duration: {terms} Months").Style(_bodyStyle);
                            }
                            else 
                            {
                                decimal saleCost = root.TryGetProperty("SalePrice", out var sProp) ? sProp.GetDecimal() : 0;
                                col.Item().Text($"Total Structural Acquisition Purchase Price: R {saleCost.ToString("N2")} (Excl VAT)").Style(_headerStyle);
                            }

                            // Output image choice thumbnails loop grid container inside the brochure canvas sheet
                            if (selectedImages.Any())
                            {
                                col.Item().PaddingTop(20).Text("AGENCY REGISTERED IMAGE ATTACHMENTS (MAX 5)").Style(_sectionHeaderStyle);
                                col.Item().Grid(grid =>
                                {
                                    grid.Spacing(10);
                                    grid.Columns(3);
                                    foreach (var imgName in selectedImages)
                                    {
                                        grid.Item().Height(80).Background("#E2E8F0").Border(1).BorderColor("#CBD5E1").Padding(5).AlignCenter().Text(imgName).Style(_bodyStyle);
                                    }
                                });
                            }
                        });
                    });
                    // --- PAGE 4+: GRANULAR SUB-UNIT INDIVIDUAL BREAKDOWN MATRIX SHEETS ---
                    foreach (var unit in subUnits)
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(1.5f, Unit.Centimetre);
                            page.Background("#F8FAFC");

                            string uNumber = unit.GetProperty("UnitNumber").GetString() ?? "Suite";
                            string uDesc = unit.GetProperty("UnitDescription").GetString() ?? "No Space Log Description Provided";
                            decimal uArea = unit.GetProperty("NetRentableAreaSqM").GetDecimal();

                            page.Header().PaddingBottom(10).Text($"SUB-UNIT OVERVIEW DISCLOSURE: {uNumber}").Style(_headerStyle);

                            page.Content().Column(col =>
                            {
                                col.Spacing(12);
                                col.Item().Text($"Net Rentable Area Space Dimension (NRA): {uArea.ToString("F2")} m²").Style(_bodyStyle);
                                col.Item().PaddingTop(40).Text("Functional Configuration Specifications Description:").Style(_sectionHeaderStyle);
                                col.Item().Text(uDesc).Style(_bodyStyle);

                                col.Item().PaddingTop(40).Height(150).Background("#E2E8F0").Border(1).BorderColor("#CBD5E1").Padding(10)
                                    .AlignCenter().Text($"[Sub-Unit Specific Architectural Image Frame for {uNumber}]").Style(_metaStyle);
                            });
                        });
                    }
                }).GeneratePdf();

                // 3. PERSIST THE COMPILED BINARY STORAGE ARTIFACT RECORD ONTO DISK VOLUMES
                string destinationDirectory = Path.Combine(AppContext.BaseDirectory, "GeneratedBrochures");
                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

                string finalFilePath = Path.Combine(destinationDirectory, $"Brochure_Deal_{dealId}.pdf");
                await File.WriteAllBytesAsync(finalFilePath, compiledDocumentBytes);

                _logger.LogInformation($"[COMPILATION SUCCESSFUL]: PDF Brochure payload saved to target node disk: '{finalFilePath}'");
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
