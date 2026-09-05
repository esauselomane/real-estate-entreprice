using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public class UserAccessClaim
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Granular platform permissions matching your configuration panel
        public bool CanDeleteProperties { get; set; } = false;
        public bool CanExportFinancialReports { get; set; } = true;
        public bool CanBypassValuationStages { get; set; } = false;

        [ForeignKey("UserId")]
        public UserAccount? AccountDetails { get; set; }
    }
}
