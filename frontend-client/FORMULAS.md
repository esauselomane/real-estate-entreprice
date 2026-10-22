# Frontend Client - Query Formulas & Display Logic

This document maps frontend components to backend query formulas defined in each microservice's `FORMULAS.md`. It also documents derived calculations performed on the client.

---

## Authentication & Tenant Context

### Current Tenant Resolution
```javascript
// Priority order:
1. Active agency override (from AgencyContext) - if set
2. JWT claim: TenantId
3. Fallback: first accessible tenant from /api/system/tenants
```

### Current User Role
```javascript
// From JWT claims:
const role = jwtClaims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
  || jwtClaims["role"];

// Normalized:
const canonicalRole = {
  "SuperAdmin": "PlatformAdmin",
  "Manager": "PlatformAdmin",
  "Admin": "AgencyAdmin",
  "AgencyAdmin": "AgencyAdmin",
  "Broker": "Broker",
  "Owner": "Owner",
  "Tenant": "Tenant"
}[role] || role;
```

---

## LeadsPage (`/crm`)

### Active Leads Count
**Backend:** `GET /api/crm/leads` + `X-Aggregates` header
**Formula:** `Active Leads` from CRM.FORMULAS.md
**Display:** Badge on "Leads" sidebar item

### Lead Stage Aggregates
**Backend:** `X-Aggregates` header from leads list
**Formula:** `Lead Stage Aggregates` from CRM.FORMULAS.md
**Display:** Pipeline funnel chart

### Suggested Brokers
**Backend:** `GET /api/crm/brokers/suggested`
**Params:** `area`, `budgetMin`, `budgetMax`, `propertyType`
**Formula:** `Suggested Brokers (Weighted Score)` from CRM.FORMULAS.md
**Display:** Top 3 badges on create lead form (Top Match / Good Match / Fair Match)

### Lead Search/Filter
**Backend:** `GET /api/crm/leads?search=&stage=&page=`
**Formula:** `Lead Search (broker-scoped)` from CRM.FORMULAS.md

---

## BrokerDashboard (`/broker-dashboard`)

### KPI Cards
| Card | Backend Field | Formula Source |
|------|--------------|----------------|
| GCI | `metrics.gci` | `Gross Commission Income (GCI)` |
| Company Rand | `metrics.companyDollar` | `GCI × 0.7` (70/30 split) |
| Active Listings | `metrics.activeListings` | Property Service `active-listings-count` |
| Avg DOM | `metrics.averageDom` | Property Service `active-listings-count` |

### Commission Gauge
**Backend:** `metrics.monthlyTargets[]` from `GET /api/crm/broker-metrics`
**Formula:** `Broker Monthly Target Progress` from CRM.FORMULAS.md
- **Min:** 0
- **Max:** `monthlyTargets[0].target` (current month target)
- **Realised:** `monthlyTargets[0].realised` (closed-won commission for current month)
- **Progress:** `(realised / target) × 100`

### Monthly Revenue Trend
**Backend:** `metrics.monthlyTrend[]` from `GET /api/crm/broker-metrics`
**Formula:** Monthly GCI + Company Dollar (last 6 months)

### Pipeline Funnel
**Derived on client:**
```javascript
const closedThisMonth = trend[trend.length - 1]?.closedCount || 0;
const awaiting = compliance.filter(c => c.category === 'compliance.awaiting_signature').length;
const atRisk = compliance.filter(c => ['critical','high'].includes(c.severity)).length;

pipelineData = [
  { name: 'New / Active', value: dealCount - closedThisMonth - awaiting },
  { name: 'Negotiation', value: awaiting },
  { name: 'Closed Won', value: closedThisMonth },
  { name: 'At Risk', value: atRisk }
];
```

### Broker Leaderboard / Activity Log
**Backend:** `GET /api/crm/broker-leaderboard` or `GET /api/crm/broker-activity`
**Formula:** `Broker Leaderboard` from CRM.FORMULAS.md

### Compliance & Alerts
**Backend:** `GET /api/crm/compliance-inbox`
**Formula:** `Compliance Items` from CRM.FORMULAS.md

---

## DealsPage (`/deals`)

### Deal Stage Distribution
**Backend:** `GET /api/crm/deals` + `X-Aggregates` header
**Formula:** `Deal Stage Distribution` from CRM.FORMULAS.md
**Display:** Horizontal bar chart

### Pipeline Value
**Backend:** `GET /api/crm/report/summary` → `TotalPipelineEstimatedValue`
**Formula:** `Pipeline Value` from CRM.FORMULAS.md

### Deal List
**Backend:** `GET /api/crm/deals?page=&search=&stage=`
**Tenant-scoped, broker-filtered**

### Commission Splits
**Backend:** `GET /api/crm/deals/{id}/broker-splits`
**Formula:** `DealBrokerSplit` model (stored at deal creation/update)

---

## SystemAdminWorkspace (`/admin`)

### Pipeline Summary
**Backend:** `GET /api/crm/report/summary`
**Formula:** `Pipeline Summary Report` from CRM.FORMULAS.md
**Display:** 6 metric cards (Active Leads, Valuations, Conveyancing, Closed Won, Pipeline Value, Avg DOM)

### Report Overview
**Backend:** `GET /api/crm/report/overview`
**Formula:** `Report Overview` from CRM.FORMULAS.md
**Display:** Commission trend chart, Broker leaderboard table

### User Management
**Backend:** `GET /api/auth/users` (Identity)
**Formula:** `Active Users` from Identity.FORMULAS.md

### Broker Target Setting
**Backend:** `GET/POST /api/crm/broker-targets`
**Formula:** `Broker Monthly Target Progress` from CRM.FORMULAS.md

---

## AgencyDashboard (`/agency`)

### Agency Glance Summary
**Backend:** `GET /api/crm/agency/glance`
**Formula:** Combination of:
- Total Brokers (Identity: `Brokers Only`)
- Total Leads (CRM: `Active Leads Count`)
- Total Deals (CRM: `Deal Stage Distribution`)
- Total Revenue (CRM: `Realised Commission`)

### Reports Tab
Same as SystemAdminWorkspace Reports

---

## Notifications Bell (Global)

### Unread Count
**Backend:** `GET /api/crm/notifications/unread-count`
**Formula:** `Unread Notification Count` from CRM.FORMULAS.md
**Polling:** Every 30 seconds

### Notification List
**Backend:** `GET /api/crm/notifications`
**Display:** Dropdown with category icons, "Remind Agent" action

---

## Key Client-Side Formulas

### Currency Formatting (South African Rand)
```javascript
const fmtCurrency = (n) => {
  const num = Number(n || 0);
  if (num >= 1_000_000) return `R ${(num / 1_000_000).toFixed(2)}M`;
  if (num >= 1_000) return `R ${(num / 1_000).toFixed(1)}K`;
  return `R ${num.toFixed(2)}`;
};
```

### Number Formatting (SA locale)
```javascript
const fmtNumber = (n) => Number(n || 0).toLocaleString('en-ZA');
```

### Relative Time
```javascript
const formatRelativeTime = (date) => {
  const diffMs = Date.now() - new Date(date);
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return 'just now';
  if (diffMin < 60) return `${diffMin} min ago`;
  // ... hours, days, weeks
};
```

### Month Label
```javascript
const monthLabel = (yyyymm) => {
  const [y, m] = yyyymm.split('-').map(Number);
  return new Date(y, m - 1, 1).toLocaleDateString('en-ZA', { month: 'short' });
};
```

### Gauge Color Logic
```javascript
const gaugeColor = progress >= 100 ? '#22c55e'  // green
  : progress >= 75 ? '#f59e0b'  // amber
  : '#ef4444';                  // red
```

---

## API Base URL
```javascript
// apiClient.js
baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/gateway'
```

All requests include:
- `Authorization: Bearer <jwt>` (auto-injected)
- `tenantId` query param (from active agency override or JWT)

---

## Error Handling
```javascript
// apiClient response interceptor normalizes:
- 401 → "Your session has expired. Please sign in again."
- 403 → "You do not have permission to view this resource."
- 5xx → "Service temporarily unavailable. Please try again."
- Network Error → "Network error — the server is not responding."
```

---

## Loading States
- **Page-level:** `LoadingOverlay` component (spinner + label)
- **Inline:** Skeleton loaders on cards/tables
- **Polling:** Silent background refresh (notifications, activity log)

## Public property website controls

Agency Overview includes agency-scoped Developer API key generation/revocation. Property Details includes reviewed public owner contacts, **Visible on public website** and **Hot property** controls. Property Search can filter **Owner submissions awaiting review** and includes the Land category. The separate `property-public-site/` website uses a server-held agency key; owner submissions and edits require review before publication. See its README for setup.

Listing modes now distinguish **For sale**, **To let**, **Sale & to let**, **Room share / let**, and **All**. Agencies review private/shared room offerings in Public website settings; owners choose the offering when submitting or editing their listing.

## Location selection

Property capture uses worldwide Google suggestions, exposes extracted administrative components for confirmation, and clears stale resolved metadata when editing the address. Exact area selections send internal locationIds; free text remains a compatibility option for unresolved legacy properties. Public ambiguous suburb/city queries require a qualified location selection.
