using Property.Microservice.Models;
using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.DTOs
{
        public record class PropertySearchCriteria
        {
            [Required] public List<string> LocationIds { get; set; } = [];
            [Required] public List<string> Locations { get; set; } = new List<string>();
            public string PropertyType { get; set; } = "All"; // All, Residential, Commercial, Industrial, Retail
            public int? MinSize { get; set; }
            public int? MaxSize { get; set; }
            public string ListingType { get; set; } = "All"; // All | Sale | Rent | Both | Room — filters on IsForSale/IsForRent flags
            public int? MinPrice { get; set; }
            public int? MaxPrice { get; set; }
        }
}
