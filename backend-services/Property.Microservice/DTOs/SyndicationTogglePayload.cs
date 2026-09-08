using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class SyndicationTogglePayload
        {
            public int PropertyId { get; set; }
            public int PlatformId { get; set; }
            public bool SyndicateStatus { get; set; }
        }
}
