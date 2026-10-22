using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class UpdatePropertyUnitPayload
        {
            [Required, MaxLength(50)] public string UnitNumber { get; set; } = string.Empty;
            public string UnitDescription { get; set; } = string.Empty;
            public decimal NetRentableAreaSqM { get; set; }
            public decimal? SpecificUnitRentalPrice { get; set; }
            public decimal? SpecificUnitSalePrice { get; set; }
            [Range(0, 999999999999d)] public decimal? AdvertisedDepositAmount { get; set; }
            public decimal? RentalRate { get; set; }
            public decimal? RentEscalationPercentage { get; set; }
            public int? LeaseTermMonths { get; set; }
            public bool IsCurrentlyVacant { get; set; } = true;
        }
}
