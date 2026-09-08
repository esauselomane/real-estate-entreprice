using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class DocumentUploadPayload
        {
            [Required] public string Name { get; set; } = string.Empty;        // human-friendly document name
            public string Description { get; set; } = string.Empty;             // free-form document description
            [Required] public string FileName { get; set; } = string.Empty;    // original file name from the client
            public string ContentType { get; set; } = string.Empty;
            [Required] public string Base64DataString { get; set; } = string.Empty; // raw file bytes, base64-encoded (mirrors image upload)
        }
}
