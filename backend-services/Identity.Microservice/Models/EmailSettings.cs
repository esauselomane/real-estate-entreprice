namespace Identity.Microservice.Models
{
    public class EmailSettings
    {
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Real Estate Enterprise";
        public string SupportEmail { get; set; } = string.Empty;
        public string TemplatesRootPath { get; set; } = string.Empty;
    }
}
