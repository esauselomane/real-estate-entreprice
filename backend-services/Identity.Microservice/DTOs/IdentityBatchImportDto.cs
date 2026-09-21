namespace Identity.Microservice.DTOs;

public record IdentityBatchImportRequest
{
    public string TenantId { get; init; } = string.Empty;
    public Dictionary<string, List<Dictionary<string, string>>> Sheets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public record IdentityBatchSheetResult(int Imported, int Errors, List<string> Messages);

public record IdentityBatchImportResponse
{
    public Dictionary<string, IdentityBatchSheetResult> Sheets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
