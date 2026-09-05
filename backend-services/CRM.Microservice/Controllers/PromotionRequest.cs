namespace CRM.Microservice.Controllers
{
        public class PromotionRequest
        {
            public bool IsRent { get; set; } = false;
            public string? PropertyTypeOverride { get; set; }
            public string? PreferredAreaOverride { get; set; }
            public int? MinSize { get; set; }
            public int? MaxSize { get; set; }
            public int? LinkToPropertyId { get; set; }
            public string? SellerNameIfNewDeal { get; set; }
        }
}
