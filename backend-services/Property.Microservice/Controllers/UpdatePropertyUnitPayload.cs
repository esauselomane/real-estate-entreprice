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
        public class UpdatePropertyUnitPayload
        {
            [Required, MaxLength(50)] public string UnitNumber { get; set; } = string.Empty;
            public string UnitDescription { get; set; } = string.Empty;
            public decimal NetRentableAreaSqM { get; set; }
            public decimal? SpecificUnitRentalPrice { get; set; }
            public decimal? SpecificUnitSalePrice { get; set; }
            public decimal? RentalRate { get; set; }
            public decimal? RentEscalationPercentage { get; set; }
            public int? LeaseTermMonths { get; set; }
            public bool IsCurrentlyVacant { get; set; } = true;
        }
}
