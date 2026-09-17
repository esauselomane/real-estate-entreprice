using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class DocumentMeta
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;   // original name (for download / display)
            public string StoredName { get; set; } = string.Empty;  // on-disk file name (for retrieval)
            public string ContentType { get; set; } = string.Empty;
            public long SizeBytes { get; set; }
            public DateTime UploadedAt { get; set; }
            public bool IsPasswordProtected { get; set; }
        }
}
