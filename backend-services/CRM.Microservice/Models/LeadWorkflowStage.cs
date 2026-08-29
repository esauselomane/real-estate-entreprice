using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public enum LeadWorkflowStage
    {
        Contacted,
        AttemptedContact,
        Nurturing,
        Disqualified,
        Qualified
    }
}
