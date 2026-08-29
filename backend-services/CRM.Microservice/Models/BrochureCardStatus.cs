using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public enum BrochureCardStatus
    {
        Active,
        Generating,
        Generated,
        Failed
    }
}
