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
        public class CreatePropertyPayload
        {
            public string CompleteAddress { get; set; } = string.Empty;
            public string ErfSpatialSize { get; set; } = string.Empty;
            public string TitleDeedType { get; set; } = "Freehold";
            public string PortfolioZoning { get; set; } = "Commercial Zone";
            public string MarketClass { get; set; } = "Sale";
            public int PropertyTypeId { get; set; } = 1;
            public decimal ValuationPrice { get; set; }
            public decimal? SalePrice { get; set; }
            public decimal? BaseRentalRate { get; set; }
            public decimal? RentEscalationPercentage { get; set; }
            public int? LeaseTermMonths { get; set; }
            public decimal TenantInstallationAllowance { get; set; } = 0;
            public decimal Latitude { get; set; } = -33.9249m;
            public decimal Longitude { get; set; } = 18.4241m;
            public string LinkedOwnerId { get; set; } = "OT-0001";
            public List<CreatePropertyUnitPayload> SubUnits { get; set; } = new List<CreatePropertyUnitPayload>();
        }
}
