namespace Property.Microservice.Controllers
{
        public class ImageUploadPayload
        {
            public string FileName { get; set; } = string.Empty;
            public string Base64DataString { get; set; } = string.Empty;
        }
}
