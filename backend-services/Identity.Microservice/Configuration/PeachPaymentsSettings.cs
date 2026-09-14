namespace Identity.Microservice.Configuration
{
    public class PeachPaymentsSettings
    {
        public const string SectionName = "PeachPayments";

        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.sandbox.peachpayments.com";
        public string CheckoutBaseUrl { get; set; } = "https://checkout.sandbox.peachpayments.com";
        public string PortalBaseUrl { get; set; } = "https://billing.sandbox.peachpayments.com";
    }
}