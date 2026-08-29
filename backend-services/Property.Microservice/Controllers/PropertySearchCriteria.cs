using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

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
