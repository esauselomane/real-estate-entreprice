namespace DataImport.Worker.Models
{
    public class ImportJobMessage
    {
        public string JobId { get; set; } = string.Empty;
        public string ImportType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Base64Content { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
