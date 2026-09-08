using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
    public class ImportJob
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string JobId { get; set; } = string.Empty;

        [Required]
        public string ImportType { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;

        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }

        public string ErrorsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
