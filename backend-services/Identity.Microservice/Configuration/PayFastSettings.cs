namespace Identity.Microservice.Configuration
{
    public class PayFastSettings
    {
        public const string SectionName = "PayFast";

        public string MerchantId { get; set; } = string.Empty;
        public string MerchantKey { get; set; } = string.Empty;
        public string PassPhrase { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://sandbox.payfast.co.za";
        public string ProcessUrl { get; set; } = "https://sandbox.payfast.co.za/eng/process";
        public string SubscriptionsUrl { get; set; } = "https://api.payfast.co.za/subscriptions";
        public string WebhookUrl { get; set; } = "https://api.payfast.co.za/webhooks";
    }
}