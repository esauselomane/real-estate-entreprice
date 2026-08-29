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
        public class PropertyMatchDto
        {
            public int PropertyId { get; set; }
            public string? LinkedOwnerId { get; set; }
            public decimal ValuationPrice { get; set; }
        }
}
