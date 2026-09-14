# Property Microservice - Query Formulas & Business Logic

## Properties

### Active Listings (Tenant-scoped)
```sql
SELECT * FROM Property
WHERE TenantId = @currentTenantId
  AND Status = 'Active'
  AND IsDeleted = 0
```

### Active Listings Count (for Broker Dashboard)
```sql
SELECT 
  COUNT(*) as ActiveListings,
  AVG(DATEDIFF(DAY, CreatedAt, GETUTCDATE())) as AverageDaysOnMarket
FROM Property
WHERE TenantId = @currentTenantId
  AND Status = 'Active'
  AND IsDeleted = 0
  {AND LinkedBrokerIds CONTAINS @brokerId} -- optional broker filter
```

### Property by ID
```sql
SELECT * FROM Property
WHERE PropertyId = @propertyId
  AND TenantId = @currentTenantId
```

### Sole Mandates Count
```sql
SELECT COUNT(*) as TotalSoleMandates
FROM Property
WHERE TenantId IN @tenantIds
  AND MandateType = 'Sole'
  AND Status = 'Active'
```

### Property Growth (6 Months)
```sql
SELECT 
  FORMAT(CreatedAt, 'yyyy-MM') as Month,
  COUNT(*) as PropertyCount
FROM Property
WHERE TenantId IN @tenantIds
  AND CreatedAt >= DATEADD(MONTH, -6, GETUTCDATE())
GROUP BY FORMAT(CreatedAt, 'yyyy-MM')
ORDER BY Month
```

---

## Property Images

### Primary Image
```sql
SELECT * FROM PropertyImage
WHERE PropertyId = @propertyId
  AND IsPrimary = 1
  AND IsDeleted = 0
```

### All Images
```sql
SELECT * FROM PropertyImage
WHERE PropertyId = @propertyId
  AND IsDeleted = 0
ORDER BY DisplayOrder
```

---

## Syndication

### Active Syndication Platforms
```sql
SELECT * FROM SyndicationPlatform
WHERE IsActive = 1
```

### Property Syndication Status
```sql
SELECT sp.Name as PlatformName, ps.Status, ps.LastSyncedAt, ps.ExternalId
FROM PropertySyndication ps
JOIN SyndicationPlatform sp ON sp.PlatformId = ps.PlatformId
WHERE ps.PropertyId = @propertyId
  AND ps.TenantId = @currentTenantId
```

---

## Brochure Generation

### Brochure Request
```json
{
  "DealId": 123,
  "BrochureCardId": 456,
  "PropertyId": 789,
  "TenantId": "TENANT-001",
  "RequestedBy": "USR-001",
  "BrokerName": "John Doe",
  "BrokerEmail": "john@agency.com",
  "BrokerPhone": "+27 11 123 4567",
  "CompanyName": "ABC Real Estate",
  "CompanyLogoUrl": "https://...",
  "ClientName": "Jane Smith",
  "ClientEmail": "jane@client.com",
  "ClientPhone": "+27 82 123 4567",
  "MarketClass": "Rental",
  "TenantInstallationAllowance": 50000,
  "SubUnits": [
    {
      "PropertyId": 789,
      "PropertyDescription": "Office park",
      "Latitude": -26.1234,
      "Longitude": 28.5678,
      "UnitNumber": "A-101",
      "UnitDescription": "Ground floor office",
      "NetRentableAreaSqM": 150.5,
      "RentalRate": 180.00,
      "RentEscalationPercentage": 7.5,
      "LeaseTermMonths": 36,
      "SalePrice": null,
      "SelectedImageRefs": ["https://..."]
    }
  ]
}
```

### Template Location
Uses external template from: `templates/brochure/document.html` with partials:
- `templates/brochure/partials/cover.html`
- `templates/brochure/partials/map-page.html`
- `templates/brochure/partials/unit.html`

---

## Amenities

### Amenity Types
```sql
SELECT * FROM AmenityType
WHERE IsActive = 1
ORDER BY Category, Name
```

### Property Amenities
```sql
SELECT at.Name, at.Category, pa.Value
FROM PropertyAmenity pa
JOIN AmenityType at ON at.AmenityTypeId = pa.AmenityTypeId
WHERE pa.PropertyId = @propertyId
```

---

## Reports

### Property Portfolio Summary
| Metric | Formula |
|--------|---------|
| TotalProperties | `COUNT(Property WHERE Status = 'Active')` |
| SoleMandates | `COUNT(Property WHERE MandateType = 'Sole')` |
| AvgDaysOnMarket | `AVG(DATEDIFF(DAY, CreatedAt, GETUTCDATE()))` |
| GrowthTrend | Monthly count over 6 months |

---

## Configuration

```json
{
  "Syndication": {
    "DefaultPlatforms": ["Property24", "PrivateProperty", "ReMax"],
    "RetryAttempts": 3,
    "RetryDelaySeconds": 30
  },
  "Images": {
    "MaxFileSizeMB": 10,
    "AllowedFormats": ["jpg", "jpeg", "png", "webp"],
    "ThumbnailSizes": [150, 300, 600, 1200]
  },
  "Brochure": {
    "OutputDirectory": "GeneratedBrochures",
    "PaperFormat": "A4"
  }
}
```

---

## Frontend Mapping

| Frontend Component | Backend Endpoint | Formula Reference |
|-------------------|------------------|-------------------|
| Property Search | `GET /api/properties` | Active Listings |
| Property Details | `GET /api/properties/{id}` | Property by ID |
| Broker Dashboard (Listings) | `GET /properties/active-listings-count` | Active Listings Count |
| Broker Dashboard (DOM) | `GET /properties/active-listings-count` | Average Days on Market |
| AgencyDashboard (Sole Mandates) | `GET /properties/stats/sole-mandates` | Sole Mandates Count |
| AgencyDashboard (Growth) | `GET /properties/stats/growth` | Property Growth |
| Syndication Tab | `POST /api/properties/brochure/generate` | Brochure Request |
| Amenities | `GET /api/properties/amenities` | Amenity Types |