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
        public class SyndicationTogglePayload
        {
            public int PropertyId { get; set; }
            public int PlatformId { get; set; }
            public bool SyndicateStatus { get; set; }
        }
}
