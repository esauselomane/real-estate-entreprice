using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class ImageUploadPayload
        {
            public string FileName { get; set; } = string.Empty;
            public string Base64DataString { get; set; } = string.Empty;
        }
}
