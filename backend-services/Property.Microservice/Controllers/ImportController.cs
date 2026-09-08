using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Property.Microservice.Controllers
{
    [ApiController]
    [Route("api/properties/import")]
    [Authorize(Policy = "PlatformAdminOnly")]
    public class ImportController : ControllerBase
    {
        private readonly PropertyDbContext _dbContext;
        private readonly KafkaPublisherService _kafka;
        private readonly ILogger<ImportController> _logger;
        private readonly IConfiguration _configuration;

        public ImportController(PropertyDbContext dbContext, KafkaPublisherService kafka, ILogger<ImportController> logger, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _kafka = kafka;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();

            // Sheet 1: Properties
            var propWs = workbook.AddWorksheet("Properties");
            propWs.Cell(1, 1).Value = "Address";
            propWs.Cell(1, 2).Value = "ErfSize";
            propWs.Cell(1, 3).Value = "TitleDeedType";
            propWs.Cell(1, 4).Value = "Zoning";
            propWs.Cell(1, 5).Value = "PropertyTypeId";
            propWs.Cell(1, 6).Value = "ValuationPrice";
            propWs.Cell(1, 7).Value = "SalePrice";
            propWs.Cell(1, 8).Value = "IsForSale";
            propWs.Cell(1, 9).Value = "IsForRent";
            propWs.Cell(1, 10).Value = "Latitude";
            propWs.Cell(1, 11).Value = "Longitude";
            propWs.Cell(1, 12).Value = "Description";
            propWs.Cell(1, 13).Value = "OwnerId";
            propWs.Range(1, 1, 1, 13).Style.Font.Bold = true;
            propWs.Cell(2, 1).Value = "123 Main Road, Cape Town";
            propWs.Cell(2, 2).Value = "250 m²";
            propWs.Cell(2, 3).Value = "Freehold";
            propWs.Cell(2, 4).Value = "Residential Zone";
            propWs.Cell(2, 5).Value = 1;
            propWs.Cell(2, 6).Value = 850000;
            propWs.Cell(2, 7).Value = 925000;
            propWs.Cell(2, 8).Value = true;
            propWs.Cell(2, 9).Value = false;
            propWs.Cell(2, 10).Value = -33.9249;
            propWs.Cell(2, 11).Value = 18.4241;
            propWs.Cell(2, 12).Value = "Beautiful family home";
            propWs.Cell(2, 13).Value = "OT-0001";

            // Sheet 2: Units
            var unitWs = workbook.AddWorksheet("Units");
            unitWs.Cell(1, 1).Value = "PropertyRef";
            unitWs.Cell(1, 2).Value = "UnitNumber";
            unitWs.Cell(1, 3).Value = "UnitDescription";
            unitWs.Cell(1, 4).Value = "NetRentableAreaSqM";
            unitWs.Cell(1, 5).Value = "SpecificUnitRentalPrice";
            unitWs.Cell(1, 6).Value = "SpecificUnitSalePrice";
            unitWs.Cell(1, 7).Value = "RentalRate";
            unitWs.Cell(1, 8).Value = "RentEscalationPercentage";
            unitWs.Cell(1, 9).Value = "LeaseTermMonths";
            unitWs.Cell(1, 10).Value = "IsCurrentlyVacant";
            unitWs.Range(1, 1, 1, 10).Style.Font.Bold = true;
            unitWs.Cell(2, 1).Value = "123 Main Road, Cape Town";
            unitWs.Cell(2, 2).Value = "Unit 1";
            unitWs.Cell(2, 3).Value = "Suite 1";
            unitWs.Cell(2, 4).Value = 95;
            unitWs.Cell(2, 5).Value = 7500;
            unitWs.Cell(2, 6).Value = 1800000;
            unitWs.Cell(2, 7).Value = 7000;
            unitWs.Cell(2, 8).Value = 7.5;
            unitWs.Cell(2, 9).Value = 12;
            unitWs.Cell(2, 10).Value = true;

            // Sheet 3: Contacts
            var contactWs = workbook.AddWorksheet("Contacts");
            contactWs.Cell(1, 1).Value = "FullName";
            contactWs.Cell(1, 2).Value = "Email";
            contactWs.Cell(1, 3).Value = "Phone";
            contactWs.Cell(1, 4).Value = "CompanyName";
            contactWs.Cell(1, 5).Value = "RecordType";
            contactWs.Cell(1, 6).Value = "PrimaryLocation";
            contactWs.Cell(1, 7).Value = "BudgetLimit";
            contactWs.Cell(1, 8).Value = "PropertyTypeInterests";
            contactWs.Cell(1, 9).Value = "AreaInterestIds";
            contactWs.Cell(1, 10).Value = "Notes";
            contactWs.Range(1, 1, 1, 10).Style.Font.Bold = true;
            contactWs.Cell(2, 1).Value = "John Doe";
            contactWs.Cell(2, 2).Value = "john@example.com";
            contactWs.Cell(2, 3).Value = "+27123456789";
            contactWs.Cell(2, 4).Value = "Acme Corp";
            contactWs.Cell(2, 5).Value = "Owner";
            contactWs.Cell(2, 6).Value = "Cape Town";
            contactWs.Cell(2, 7).Value = 1500000;
            contactWs.Cell(2, 8).Value = "[\"House\",\"Apartment\"]";
            contactWs.Cell(2, 9).Value = "[1,2,3]";
            contactWs.Cell(2, 10).Value = "Looking in Sea Point";

            foreach (var ws in new[] { propWs, unitWs, contactWs })
                ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "property-import-template.xlsx");
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadExcel([FromBody] ImportUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64Content))
                return BadRequest(new { message = "FileName and Base64Content are required." });

            var jobId = Guid.NewGuid().ToString("N");
            var caller = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var tenantId = GetCurrentTenant();

            var payload = new
            {
                JobId = jobId,
                FileName = request.FileName,
                Base64Content = request.Base64Content,
                RequestedBy = caller,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };

            await _kafka.RaiseEventAsync("data-import", jobId, System.Text.Json.JsonSerializer.Serialize(payload));

            var job = new ImportJob
            {
                JobId = jobId,
                ImportType = "mixed",
                Status = "queued",
                TotalRows = 0,
                ProcessedRows = 0,
                SuccessCount = 0,
                ErrorCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.ImportJobs.Add(job);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Import job {JobId} queued by {Caller}", jobId, caller);

            return Accepted(new { jobId, status = "queued" });
        }

        [HttpGet("jobs/{jobId}")]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            var job = await _dbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null) return NotFound(new { message = "Job not found." });

            var errors = string.IsNullOrEmpty(job.ErrorsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(job.ErrorsJson) ?? new List<string>();

            return Ok(new ImportJobStatus
            {
                JobId = job.JobId,
                ImportType = job.ImportType,
                Status = job.Status,
                TotalRows = job.TotalRows,
                ProcessedRows = job.ProcessedRows,
                SuccessCount = job.SuccessCount,
                ErrorCount = job.ErrorCount,
                Errors = errors,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt
            });
        }

        private string GetCurrentTenant()
        {
            return User?.FindFirst("TenantId")?.Value
                ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
                ?? string.Empty;
        }
    }
}
