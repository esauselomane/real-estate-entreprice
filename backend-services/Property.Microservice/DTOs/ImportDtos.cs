namespace Property.Microservice.DTOs
{
    public record class ImportUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string Base64Content { get; set; } = string.Empty;
    }

    public record class ImportJobStatus
    {
        public string JobId { get; set; } = string.Empty;
        public string ImportType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
