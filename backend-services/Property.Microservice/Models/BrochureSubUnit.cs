using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class BrochureSubUnit
    {
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitDescription { get; set; } = string.Empty;
        public decimal NetRentableAreaSqM { get; set; }
    }
}
