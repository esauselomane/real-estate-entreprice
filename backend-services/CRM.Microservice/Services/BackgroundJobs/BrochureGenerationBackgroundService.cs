using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Implementation;

namespace CRM.Microservice.Services.BackgroundJobs
{
    public class BrochureGenerationBackgroundService : BackgroundService
    {
        private readonly ILogger<BrochureGenerationBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IJobQueue<BrochureGenerationJob> _jobQueue;
        private readonly string _generatedBrochuresPath;

        public BrochureGenerationBackgroundService(
            ILogger<BrochureGenerationBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IJobQueue<BrochureGenerationJob> jobQueue,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobQueue = jobQueue;
            _generatedBrochuresPath = configuration["Brochure:OutputPath"] 
                ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GeneratedBrochures"));
        }

        public async ValueTask EnqueueAsync(BrochureGenerationJob job, CancellationToken ct = default)
        {
            await _jobQueue.EnqueueAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BROCHURE BACKGROUND SERVICE]: Started, listening for brochure generation jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("[BROCHURE]: Processing DealId={DealId} Broker={Broker} Client={Client} Units={Units}",
                        job.DealId, job.BrokerName, job.ClientName, job.SubUnits.Count);

                    var result = await ProcessGenerationJobAsync(job, stoppingToken);

                    // Update BrochureCard and create notification
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<CRMDbContext>();
                    var emailService = scope.ServiceProvider.GetService<IEmailNotificationService>();

                    var card = await dbContext.BrochureCards
                        .FirstOrDefaultAsync(c => c.CardId == job.BrochureCardId, stoppingToken);

                    if (card != null)
                    {
                        card.Status = result.Success ? BrochureCardStatus.Generated : BrochureCardStatus.Failed;
                        card.GeneratedFilePath = result.PdfPath ?? string.Empty;
                        card.UpdatedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    // Create in-app notification
                    var notification = new Notification
                    {
                        TenantId = job.TenantId,
                        UserId = job.RequestedBy,
                        Category = "brochure",
                        Title = result.Success ? "Brochure generated successfully" : "Brochure generation failed",
                        Body = result.Success
                            ? $"Your brochure for deal {job.DealId} is ready."
                            : $"Brochure generation failed: {result.ErrorMessage ?? "Unknown error"}",
                        LinkType = "brochure",
                        LinkId = job.BrochureCardId.ToString(),
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Notifications.Add(notification);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    // Send email notification if successful
                    if (result.Success && emailService != null && card != null)
                    {
                        try
                        {
                            var brokerRepo = scope.ServiceProvider.GetRequiredService<Services.Abstractions.IBrokerRepository>();
                            var broker = await brokerRepo.GetByIdAsync(job.RequestedBy, stoppingToken);
                            if (broker != null && !string.IsNullOrEmpty(broker.Email))
                            {
                                var downloadUrl = $"/api/crm/brochure-cards/{job.BrochureCardId}/download";
                                await emailService.SendBrochureGeneratedEmailAsync(
                                    toEmail: broker.Email,
                                    toName: broker.FullName,
                                    brokerName: broker.FullName,
                                    dealId: job.DealId.ToString(),
                                    dealAddress: card.ClientName,
                                    generatedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                                    clientName: card.ClientName,
                                    marketClass: "",
                                    unitCount: card.Lines?.Count ?? 0,
                                    downloadUrl: downloadUrl,
                                    supportEmail: "support@realestate.com",
                                    ct: stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send brochure email notification for DealId={DealId}", job.DealId);
                        }
                    }

                    _logger.LogInformation("[BROCHURE]: Completed DealId={DealId} Success={Success}", job.DealId, result.Success);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BROCHURE BACKGROUND SERVICE]: Error processing job");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task<BrochureGenerationResult> ProcessGenerationJobAsync(BrochureGenerationJob job, CancellationToken ct)
        {
            try
            {
                await new BrowserFetcher().DownloadAsync();
                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });

                var subUnits = job.SubUnits.Select(u => new BrochureSubUnit
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
                    SelectedImageRefs = u.SelectedImageRefs,
                    PropertyType = u.PropertyType ?? string.Empty,
                    ValuationPrice = u.ValuationPrice,
                    ErfSpatialSize = u.ErfSpatialSize ?? string.Empty,
                    TitleDeedType = u.TitleDeedType ?? string.Empty,
                    PortfolioZoning = u.PortfolioZoning ?? string.Empty,
                    IsForSale = u.IsForSale,
                    IsForRent = u.IsForRent,
                    IsCurrentlyVacant = u.IsCurrentlyVacant,
                    Amenities = u.Amenities ?? new List<AmenityInfo>()
                }).ToList();

                var logoBytes = !string.IsNullOrEmpty(job.CompanyLogoUrl) ? await DownloadImageBytesAsync(job.CompanyLogoUrl) : null;
                var logoDataUrl = logoBytes != null ? $"data:image/png;base64,{Convert.ToBase64String(logoBytes)}" : string.Empty;

                var html = BuildBrochureHtml(
                    job.BrokerName, job.BrokerEmail, job.BrokerPhone,
                    job.CompanyName, logoDataUrl,
                    job.ClientName, job.ClientEmail, job.ClientPhone,
                    job.MarketClass, job.TenantInstallationAllowance,
                    subUnits);

                byte[] compiledDocumentBytes;
                using (var page = await browser.NewPageAsync())
                {
                    await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
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

                if (!Directory.Exists(_generatedBrochuresPath)) Directory.CreateDirectory(_generatedBrochuresPath);
                string finalFilePath = Path.Combine(_generatedBrochuresPath, $"Brochure_Deal_{job.DealId}.pdf");
                await File.WriteAllBytesAsync(finalFilePath, compiledDocumentBytes);

                _logger.LogInformation("[BROCHURE]: PDF saved to {Path}", finalFilePath);
                return new BrochureGenerationResult(true, finalFilePath, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BROCHURE]: Generation failed for DealId={DealId}", job.DealId);
                return new BrochureGenerationResult(false, null, ex.Message);
            }
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

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(text);
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

        private record BrochureSubUnit
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
            public string PropertyType { get; set; } = string.Empty;
            public decimal? ValuationPrice { get; set; }
            public string? ErfSpatialSize { get; set; }
            public string? TitleDeedType { get; set; }
            public string? PortfolioZoning { get; set; }
            public bool IsForSale { get; set; }
            public bool IsForRent { get; set; }
            public bool IsCurrentlyVacant { get; set; }
            public List<AmenityInfo> Amenities { get; set; } = new();
        }
    }
}