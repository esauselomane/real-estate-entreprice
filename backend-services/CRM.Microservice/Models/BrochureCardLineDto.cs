using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class BrochureCardLineDto
    {
        public int PropertyId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitDescription { get; set; } = string.Empty;
        public string PropertyDescription { get; set; } = string.Empty;
        public decimal NetRentableAreaSqM { get; set; }
        public List<string> SelectedImageRefs { get; set; } = new();
    }
}
