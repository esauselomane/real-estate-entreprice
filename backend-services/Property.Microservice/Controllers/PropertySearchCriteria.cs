namespace Property.Microservice.Controllers
{
        public class PropertySearchCriteria
        {
            public List<string> Locations { get; set; } = new List<string>();
            public string PropertyType { get; set; } = "All"; // All, Residential, Commercial, Industrial, Retail
            public int? MinSize { get; set; }
            public int? MaxSize { get; set; }
            public string ListingType { get; set; } = "All"; // All | Sale | Rent — filters on IsForSale/IsForRent flags
            public int? MinPrice { get; set; }
            public int? MaxPrice { get; set; }
        }
}
