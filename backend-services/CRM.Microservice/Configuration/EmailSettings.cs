namespace CRM.Microservice.Configuration;

public class EmailSettings
{
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Biscove Technologies";
    public string SupportEmail { get; set; } = string.Empty;
    public string TemplatesRootPath { get; set; } = string.Empty;
}
