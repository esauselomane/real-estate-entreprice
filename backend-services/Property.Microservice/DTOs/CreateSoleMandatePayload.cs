using System.ComponentModel.DataAnnotations;
using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
    // 📥 Body for POST /properties/{id}/mandates. Dates are required;
    // a 12-month default is applied client-side if the user doesn't pick.
    public record class CreateSoleMandatePayload
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }
}
