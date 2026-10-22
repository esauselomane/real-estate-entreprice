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

## Public website publication

`IsPublicListing && PublicReviewStatus == "Approved"` determines Developer API visibility. `IsHotProperty` is effective only while a listing is public. Owner submissions and owner edits set visibility/hot flags to false and review status to `Pending review`; an authorised agency editor approves through `PUT /api/properties/{id}/public-listing`. Public owner name/email/phone are a reviewed publication snapshot. Existing properties start unpublished.

Nearby results use great-circle distance with Earth mean radius 6,371.0088 km; the configurable radius is 1–100 km. Text searches use a known approved listing in the searched area as an anchor and display exact area matches before nearby listings. See `property-public-site/README.md` for endpoints, configuration and the shared property-database boundary.

Room offerings use `RoomLettingType = None | PrivateRoom | SharedRoom`, independently of property type. Private/shared room submissions require a monthly unit price and set `IsForRent = true`. Search mode `Both` requires `IsForSale && IsForRent`; `Room` requires a room offering and uses unit rental prices. Publication approval and owner-edit withdrawal rules apply to rooms as well.

## Confirmed location identity

LocationId = SHA-256(JSON([countryCode, region, district, city, suburb])), with whitespace collapsed, Unicode NFC normalization and case-insensitive identity. Country names and street addresses are not keys. Country code is required when supplying structured location details; other levels are optional. PropertyLocations reuses the resulting identity, with a SQL Server range lock preventing duplicate concurrent creation. Existing unstructured property addresses remain unresolved until confirmed; coordinates at zero are valid and never replaced with Cape Town defaults.

## Public amenities and advertised deposits

Public details include only active amenities belonging to the listing agency and active catalogue types. Property-level and unit-level amenities retain separate scopes; internal notes are omitted. Unit AdvertisedDepositAmount is an optional public asking deposit (nonnegative), never derived from a real tenant's lease DepositAmount. Null = on request; zero = no deposit required. Fictional sample deposits may use 1.5 × sample monthly rent when explicitly seeding public features.

## Room classification and hot authorization

Room share/let is a property type, not a sale/rental purpose. A PrivateRoom/SharedRoom subtype classifies the property as Room share/let without enabling rental automatically. Catalogue upgrades reclassify existing room offerings without changing purpose or visibility flags. Hot status is mutable only when the authenticated canonical role is AgencyAdmin or PlatformAdmin AND the caller's TenantId equals the property's TenantId. Tenant query overrides do not participate in this authorization. Omitted hot settings preserve the previous designation; owner edits withdraw visibility without changing hot status.

## Public letting term

PublicLettingExpiresAt = agency publication time.AddMonths(2); explicit agency renewal uses renewal time.AddMonths(2). Normal edits never extend a term. A public letting is available only while its expiry is in the future. Rental-only properties are withdrawn on expiry; mixed properties retain public sale availability while public rental price/deposit fields are omitted. The expiry report uses UTC bounds and authenticated property visibility.
