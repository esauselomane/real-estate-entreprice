# CRM Microservice - Query Formulas & Business Logic

This document defines the canonical query formulas used across all lists, summaries, and reports in the CRM Microservice. These formulas ensure consistency between backend calculations and frontend displays.

## Tenant Scoping
**All queries must include tenant filtering:**
```sql
WHERE TenantId IN (SELECT tenant_id FROM visible_tenants_for_current_user())
```
- PlatformAdmin: all tenants
- AgencyAdmin: own tenant only
- Broker: own tenant only

---

## Leads

### Active Leads
```sql
SELECT * FROM LeadRecord
WHERE TenantId = @currentTenantId
  AND LeadStage != 'Disqualified'
  AND IsConvertedToDeal = 0
```

### Lead Stage Aggregates
```sql
SELECT LeadStage, COUNT(*) as Count
FROM LeadRecord
WHERE TenantId = @currentTenantId
  AND IsConvertedToDeal = 0
GROUP BY LeadStage
```

### Lead Search (broker-scoped)
```sql
SELECT * FROM LeadRecord
WHERE TenantId = @currentTenantId
  AND IsConvertedToDeal = 0
  AND (
    @role = 'Broker' AND AllocatedBrokerIds CONTAINS @callerId
    OR @role IN ('AgencyAdmin', 'PlatformAdmin')
  )
```

---

## Deals

### Pipeline Value (Active Deals)
```sql
SELECT SUM(TransactionValue) as TotalPipelineValue
FROM DealRecord
WHERE TenantId = @currentTenantId
  AND DealStage IN ('DiscoveryNeedsAnalysis', 'Presentation', 'ProposalQuoteSent', 'Negotiation')
```

### Closed Won Deals (Current Month)
```sql
SELECT COUNT(*) as ClosedWonCount
FROM DealRecord
WHERE TenantId = @currentTenantId
  AND DealStage = 'ClosedWon'
  AND MONTH(LastStageUpdatedAt) = MONTH(GETUTCDATE())
  AND YEAR(LastStageUpdatedAt) = YEAR(GETUTCDATE())
```

### Deal Stage Distribution
```sql
SELECT DealStage, COUNT(*) as Count
FROM DealRecord
WHERE TenantId = @currentTenantId
GROUP BY DealStage
```

### Broker-Assigned Deals
```sql
SELECT * FROM DealRecord
WHERE TenantId = @currentTenantId
  AND AllocatedBrokerIds CONTAINS @brokerId
```

---

## Commissions

### Gross Commission Income (GCI) - All Time
```sql
SELECT SUM(
  CASE 
    WHEN CommissionType = 'Percent' THEN TransactionValue * CommissionValue / 100
    ELSE CommissionValue
  END
) as GCI
FROM DealRecord
WHERE TenantId = @currentTenantId
```

### Realised Commission (Closed Won Only)
```sql
SELECT SUM(
  COALESCE(RealisedCommission,
    CASE 
      WHEN CommissionType = 'Percent' THEN TransactionValue * CommissionValue / 100
      ELSE CommissionValue
    END
  )
) as RealisedCommission
FROM DealRecord
WHERE TenantId = @currentTenantId
  AND DealStage = 'ClosedWon'
```

### Monthly Realised Commission (by Close Month)
```sql
SELECT 
  YEAR(LastStageUpdatedAt) as Year,
  MONTH(LastStageUpdatedAt) as Month,
  SUM(
    COALESCE(RealisedCommission,
      CASE 
        WHEN CommissionType = 'Percent' THEN TransactionValue * CommissionValue / 100
        ELSE CommissionValue
      END
    )
  ) as MonthlyRealised
FROM DealRecord
WHERE TenantId = @currentTenantId
  AND DealStage = 'ClosedWon'
GROUP BY YEAR(LastStageUpdatedAt), MONTH(LastStageUpdatedAt)
ORDER BY Year, Month
```

### Broker Monthly Target Progress
```sql
SELECT 
  bmt.Year,
  bmt.Month,
  bmt.TargetCommission as Target,
  COALESCE(mrc.MonthlyRealised, 0) as Realised,
  CASE WHEN bmt.TargetCommission = 0 THEN 0 
       ELSE ROUND(COALESCE(mrc.MonthlyRealised, 0) / bmt.TargetCommission * 100, 1)
  END as ProgressPercentage
FROM BrokerMonthlyTarget bmt
LEFT JOIN (
  -- Monthly Realised Commission subquery from above
) mrc ON mrc.Year = bmt.Year AND mrc.Month = bmt.Month AND mrc.BrokerId = bmt.BrokerId
WHERE bmt.BrokerId = @brokerId
  AND bmt.TenantId = @currentTenantId
ORDER BY bmt.Year DESC, bmt.Month DESC
```

---

## Broker Performance

### Broker Leaderboard (Tenant-scoped)
```sql
SELECT 
  b.BrokerId,
  b.FullName as Name,
  COUNT(d.DealId) as DealsClosed,
  SUM(
    CASE 
      WHEN d.CommissionType = 'Percent' THEN d.TransactionValue * d.CommissionValue / 100
      ELSE d.CommissionValue
    END
  ) as TotalGCI,
  COUNT(p.PropertyId) as ActiveListings
FROM Broker b
LEFT JOIN DealRecord d ON d.AllocatedBrokerIds CONTAINS b.BrokerId 
  AND d.DealStage = 'ClosedWon' 
  AND d.TenantId = b.TenantId
LEFT JOIN Property p ON p.LinkedBrokerIds CONTAINS b.BrokerId 
  AND p.TenantId = b.TenantId
WHERE b.TenantId = @currentTenantId
GROUP BY b.BrokerId, b.FullName
ORDER BY TotalGCI DESC
```

### Suggested Brokers (Weighted Score)
Weights from `appsettings.json:BrokerScoring:Weights`:
- ConversionRate: 30%
- DaysToClose: 15%
- CommissionGenerated: 15%
- StageVelocity: 10%
- GeoSpecialization: 10%
- PropertyType: 10%
- PriceBand: 5%
- CrmHygiene: 5%

```sql
-- Computed nightly by BrokerMetricsBackgroundJob
SELECT * FROM BrokerPerformanceMetrics
WHERE TenantId = @currentTenantId
ORDER BY WeightedScore DESC
LIMIT 3
```

---

## Properties (via Property Service)

### Active Listings Count
```http
GET /properties/active-listings-count?tenantId={tenantIds}&linkedBrokerId={brokerId}
```

### Average Days on Market
```http
GET /properties/active-listings-count?tenantId={tenantIds}&linkedBrokerId={brokerId}
-- Returns: { activeListings, averageDom }
```

---

## Notifications

### Unread Notification Count
```sql
SELECT COUNT(*) FROM Notification
WHERE UserId = @currentUserId
  AND IsRead = 0
```

### Compliance Items (Expiring Mandates + Awaiting Signature)
```sql
-- Expiring sole mandates (within 30 days)
SELECT * FROM Mandate
WHERE TenantId = @currentTenantId
  AND MandateType = 'Sole'
  AND ExpiryDate BETWEEN GETUTCDATE() AND DATEADD(DAY, 30, GETUTCDATE())

-- Awaiting broker signature
SELECT * FROM DealRecord
WHERE TenantId = @currentTenantId
  AND DealStage = 'ProposalQuoteSent'
  AND RequiresBrokerSignature = 1
```

---

## Reports

### Pipeline Summary Report
| Metric | Formula |
|--------|---------|
| ActiveLeadsCount | `COUNT(LeadRecord WHERE LeadStage != 'Disqualified' AND IsConvertedToDeal = 0)` |
| ValuationsInProcessCount | `COUNT(DealRecord WHERE DealStage = 'Valuation')` |
| ConveyancingAtDeedsCount | `COUNT(DealRecord WHERE DealStage = 'Conveyancing')` |
| FinalizedClosedCount | `COUNT(DealRecord WHERE DealStage = 'ClosedWon')` |
| TotalPipelineEstimatedValue | `SUM(TransactionValue) WHERE DealStage IN (Discovery, Presentation, Proposal, Negotiation)` |

### Report Overview (Yearly)
- Monthly GCI trend (6 months)
- Monthly Closed Won count
- Monthly Pipeline Value
- Broker Leaderboard

---

## Configuration References

All weights and thresholds are configurable via `appsettings.json`:

```json
{
  "BrokerScoring": {
    "Weights": { ... },
    "BackgroundJob": { "Enabled": true, "HourUtc": 2 }
  }
}
```

---

## Frontend Mapping

| Frontend Component | Backend Endpoint | Formula Reference |
|-------------------|------------------|-------------------|
| LeadsPage (active) | `GET /api/crm/leads` | Active Leads |
| LeadsPage (stage counts) | `X-Aggregates header` | Lead Stage Aggregates |
| BrokerDashboard (GCI) | `GET /api/crm/broker-metrics` | GCI |
| BrokerDashboard (Realised) | `GET /api/crm/broker-metrics` | Realised Commission |
| BrokerDashboard (Gauge) | `GET /api/crm/broker-metrics` | Broker Monthly Target Progress |
| BrokerDashboard (Leaderboard) | `GET /api/crm/broker-leaderboard` | Broker Leaderboard |
| BrokerDashboard (Compliance) | `GET /api/crm/compliance` | Compliance Items |
| SystemAdminWorkspace (Reports) | `GET /api/crm/report/summary` | Pipeline Summary Report |
| SystemAdminWorkspace (Overview) | `GET /api/crm/report/overview` | Report Overview |
| DealsPage (Pipeline) | `GET /api/crm/deals` | Deal Stage Distribution |
| DealsPage (Pipeline Value) | `GET /api/crm/report/summary` | Pipeline Value |

---

## Key Principles

1. **Tenant isolation is mandatory** - every query filters by `TenantId`
2. **Realised commission uses close date** - `LastStageUpdatedAt` month, not `CreatedAt`
3. **Broker scope** - brokers only see their allocated records; admins see tenant aggregate
4. **Configuration over hardcoding** - weights, thresholds in `appsettings.json`
5. **Nightly pre-computation** - heavy aggregates (BrokerPerformanceMetrics) computed by BackgroundService