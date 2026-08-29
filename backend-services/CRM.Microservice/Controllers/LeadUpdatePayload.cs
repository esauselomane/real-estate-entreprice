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
        public class LeadUpdatePayload { public int LeadId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; public int? LinkToPropertyId { get; set; } public string SellerNameIfNewDeal { get; set; } = string.Empty; public string CommissionType { get; set; } = CommissionTypes.Percent; public decimal CommissionValue { get; set; } = 0m; }
}
