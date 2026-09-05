namespace Property.Microservice.Controllers
{
        public class SyndicationTogglePayload
        {
            public int PropertyId { get; set; }
            public int PlatformId { get; set; }
            public bool SyndicateStatus { get; set; }
        }
}
