namespace Identity.Microservice.Models
{
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Real Estate Enterprise";
        public string SupportEmail { get; set; } = string.Empty;
        public string TemplatesRootPath { get; set; } = string.Empty;
    }
}
