using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class BrochureCardLine
    {
        [Key]
        public int LineId { get; set; }

        [Required] public int CardId { get; set; }
        [Required] public int PropertyId { get; set; }
        public int UnitId { get; set; }

        [MaxLength(50)] public string UnitNumber { get; set; } = string.Empty;
        [MaxLength(256)] public string UnitDescription { get; set; } = string.Empty;
        [MaxLength(512)] public string PropertyDescription { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal NetRentableAreaSqM { get; set; }

        public string SelectedImageRefs { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;

        [ForeignKey("CardId")]
        public BrochureCard? Card { get; set; }
    }
}
