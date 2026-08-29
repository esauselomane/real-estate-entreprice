using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public enum DealWorkflowStage
    {
        DiscoveryNeedsAnalysis,
        Presentation,
        ProposalQuoteSent,
        Negotiation,
        ClosedWon,
        ClosedLost
    }
}
