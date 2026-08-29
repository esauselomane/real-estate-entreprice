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
        public class DealUpdatePayload { public int DealId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; }
}
