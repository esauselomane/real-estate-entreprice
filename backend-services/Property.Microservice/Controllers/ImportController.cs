using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.BackgroundJobs;
using System.Text.Json;

namespace Property.Microservice.Controllers
{
    [ApiController]
    [Route("api/properties/import")]
    [Authorize(Policy = "PlatformAdminOnly")]
    public class ImportController : ControllerBase
    {
        private readonly PropertyDbContext _dbContext;
        private readonly ILogger<ImportController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IJobQueue<DataImportJob> _jobQueue;

        public ImportController(
            PropertyDbContext dbContext,
            ILogger<ImportController> logger,
            IConfiguration configuration,
            IJobQueue<DataImportJob> jobQueue)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            _jobQueue = jobQueue;
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();

            AddHeaderRow(workbook.AddWorksheet("Agencies"), ImportSheetDefinition.Agencies);
            AddSampleRow(workbook.Worksheet("Agencies"), ImportSheetDefinition.Agencies);

            AddHeaderRow(workbook.AddWorksheet("Brokers"), ImportSheetDefinition.Brokers);
            AddSampleRow(workbook.Worksheet("Brokers"), ImportSheetDefinition.Brokers);

            AddHeaderRow(workbook.AddWorksheet("Contacts"), ImportSheetDefinition.Contacts);
            AddSampleRow(workbook.Worksheet("Contacts"), ImportSheetDefinition.Contacts);

            AddHeaderRow(workbook.AddWorksheet("Companies"), ImportSheetDefinition.Companies);
            AddSampleRow(workbook.Worksheet("Companies"), ImportSheetDefinition.Companies);

            AddHeaderRow(workbook.AddWorksheet("LegalEntities"), ImportSheetDefinition.LegalEntities);
            AddSampleRow(workbook.Worksheet("LegalEntities"), ImportSheetDefinition.LegalEntities);

            AddHeaderRow(workbook.AddWorksheet("Leads"), ImportSheetDefinition.Leads);
            AddSampleRow(workbook.Worksheet("Leads"), ImportSheetDefinition.Leads);

            AddHeaderRow(workbook.AddWorksheet("Deals"), ImportSheetDefinition.Deals);
            AddSampleRow(workbook.Worksheet("Deals"), ImportSheetDefinition.Deals);

            AddHeaderRow(workbook.AddWorksheet("Geography"), ImportSheetDefinition.Geography);
            AddSampleRow(workbook.Worksheet("Geography"), ImportSheetDefinition.Geography);

            AddHeaderRow(workbook.AddWorksheet("Properties"), ImportSheetDefinition.Properties);
            AddSampleRow(workbook.Worksheet("Properties"), ImportSheetDefinition.Properties);

            AddHeaderRow(workbook.AddWorksheet("Units"), ImportSheetDefinition.Units);
            AddSampleRow(workbook.Worksheet("Units"), ImportSheetDefinition.Units);

            AddHeaderRow(workbook.AddWorksheet("PropertyTypes"), ImportSheetDefinition.PropertyTypes);
            AddSampleRow(workbook.Worksheet("PropertyTypes"), ImportSheetDefinition.PropertyTypes);

            AddHeaderRow(workbook.AddWorksheet("AmenityTypes"), ImportSheetDefinition.AmenityTypes);
            AddSampleRow(workbook.Worksheet("AmenityTypes"), ImportSheetDefinition.AmenityTypes);

            AddHeaderRow(workbook.AddWorksheet("TenantAgencies"), ImportSheetDefinition.TenantAgencies);
            AddSampleRow(workbook.Worksheet("TenantAgencies"), ImportSheetDefinition.TenantAgencies);

            AddHeaderRow(workbook.AddWorksheet("UserAccounts"), ImportSheetDefinition.UserAccounts);
            AddSampleRow(workbook.Worksheet("UserAccounts"), ImportSheetDefinition.UserAccounts);

            foreach (var ws in workbook.Worksheets)
                ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "system-data-template.xlsx");
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadExcel([FromBody] ImportUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64Content))
                return BadRequest(new { message = "FileName and Base64Content are required." });

            var jobId = Guid.NewGuid().ToString("N");
            var caller = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var tenantId = GetCurrentTenant();
            var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;

            var trackingJob = new ImportJob
            {
                JobId = jobId,
                ImportType = "system",
                Status = "queued",
                TotalRows = 0,
                ProcessedRows = 0,
                SuccessCount = 0,
                ErrorCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.ImportJobs.Add(trackingJob);
            await _dbContext.SaveChangesAsync();

            var importJob = new DataImportJob(
                jobId,
                "system",
                request.FileName,
                request.Base64Content,
                tenantId,
                caller,
                authorizationHeader);

            await _jobQueue.EnqueueAsync(importJob);

            _logger.LogInformation("System data import job {JobId} enqueued by {Caller} from file {FileName}.", jobId, caller, request.FileName);

            return Accepted(new { jobId, status = "queued" });
        }

        [HttpGet("jobs/{jobId}")]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            var job = await _dbContext.ImportJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null) return NotFound(new { message = "Job not found." });

            var errors = string.IsNullOrEmpty(job.ErrorsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(job.ErrorsJson) ?? new List<string>();

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

        private static void AddHeaderRow(ClosedXML.Excel.IXLWorksheet worksheet, ImportSheetColumn[] columns)
        {
            for (var index = 0; index < columns.Length; index++)
                worksheet.Cell(1, index + 1).Value = columns[index].ColumnName;

            worksheet.Range(1, 1, 1, columns.Length).Style.Font.Bold = true;
        }

        private static void AddSampleRow(ClosedXML.Excel.IXLWorksheet worksheet, ImportSheetColumn[] columns)
        {
            for (var index = 0; index < columns.Length; index++)
                SetCellValue(worksheet.Cell(2, index + 1), columns[index].SampleValue);
        }

        private static void SetCellValue(ClosedXML.Excel.IXLCell cell, object? value)
        {
            if (value is null or "")
                cell.Value = string.Empty;
            else if (value is int intValue)
                cell.Value = intValue;
            else if (value is bool boolValue)
                cell.Value = boolValue;
            else if (value is decimal decimalValue)
                cell.Value = decimalValue;
            else if (value is double doubleValue)
                cell.Value = doubleValue;
            else
                cell.Value = value.ToString();
        }

        private string GetCurrentTenant()
        {
            return User?.FindFirst("TenantId")?.Value
                ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
                ?? string.Empty;
        }
    }
}
