using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
