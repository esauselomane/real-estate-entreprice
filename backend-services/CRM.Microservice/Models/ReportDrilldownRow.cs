namespace CRM.Microservice.Models
{
    public class ReportDrilldownRow
    {
        public string Period { get; set; } = string.Empty;
        public int Leads { get; set; }
        public int Deals { get; set; }
        public decimal Revenue { get; set; }
    }
}
