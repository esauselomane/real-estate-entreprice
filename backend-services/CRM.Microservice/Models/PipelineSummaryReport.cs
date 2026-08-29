using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class PipelineSummaryReport
    {
        public int ActiveLeadsCount { get; set; }
        public int ValuationsInProcessCount { get; set; }
        public int ConveyancingAtDeedsCount { get; set; }
        public int FinalizedClosedCount { get; set; }
        public decimal TotalPipelineEstimatedValue { get; set; }
    }
}
