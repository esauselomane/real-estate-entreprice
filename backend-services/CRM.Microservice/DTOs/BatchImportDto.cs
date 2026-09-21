namespace CRM.Microservice.DTOs;

public record BatchSheetResult(int Imported, int Errors, List<string> Messages);

public record CrmBatchImportRequest
{
    public string TenantId { get; init; } = string.Empty;
    public Dictionary<string, List<Dictionary<string, string>>> Sheets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public record CrmBatchImportResponse
{
    public Dictionary<string, BatchSheetResult> Sheets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
