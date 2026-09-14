#!/usr/bin/env python3
"""End-to-end test: Generate a brochure with 3 units across 2 properties."""
import requests
import json
import time
import sys
import os
import pyodbc

GATEWAY = "http://localhost:5000/gateway"
CRM_BASE = "http://localhost:5003"
PROP_BASE = "http://localhost:5002"
GENERATED_BROCHURES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "backend-services", "CRM.Microservice", "GeneratedBrochures")
TENANT = "TEN-0001"
BROKER_EMAIL = "broker@agency.local"
BROKER_PASSWORD = "Password123!"
DEAL_ID = 9900
BROKER_NAME = "Test Broker"
BROKER_EMAIL_ADDR = "broker@agency.local"
BROKER_PHONE = "+27820000001"
CLIENT_NAME = "Test Client"
CLIENT_EMAIL = "client@test.com"
CLIENT_PHONE = "+27820000002"
COMPANY_NAME = "Test Agency"

session = requests.Session()
session.headers.update({"Content-Type": "application/json"})

def log(msg):
    print(f"\n{'='*60}\n🔹 {msg}\n{'='*60}", flush=True)

def api_get(url, params=None):
    r = session.get(url, params=params or {})
    return r

def api_post(url, data=None):
    r = session.post(url, json=data or {})
    return r

# ── Step 1: Login ──
log("Step 1: Logging in as broker")
r = api_post(f"{GATEWAY}/auth/login", {"email": BROKER_EMAIL, "password": BROKER_PASSWORD})
print(f"  Status: {r.status_code}")
if r.status_code != 200:
    print(f"  ERROR: {r.text}")
    sys.exit(1)
token = r.json()["token"]
profile = r.json()["profile"]
session.headers.update({"Authorization": f"Bearer {token}"})
print(f"  Token obtained for: {profile['name']} (Tenant: {profile['tenantId']}, Role: {profile['role']})")

# ── Step 2: Seed test data directly into SQL Server ──
log("Step 2: Seeding test data via SQL Server")
conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost,1433;"
    "DATABASE=RealEstateCRMDB;"
    "UID=sa;"
    "PWD=SecureRealEstatePass123!;"
    "TrustServerCertificate=yes;"
)
try:
    conn = pyodbc.connect(conn_str)
    cursor = conn.cursor()

    # Check if deal exists
    cursor.execute("SELECT COUNT(*) FROM [DealRecords] WHERE DealId = ?", DEAL_ID)
    deal_count = cursor.fetchone()[0]
    if deal_count == 0:
        log("  Creating test deal")
        cursor.execute("""
            INSERT INTO [DealRecords] ([TenantId], [AllocatedBrokerIds], [DealStage], [LinkedPropertyId],
                [BuyerClientId], [SellerClientId], [TransactionValue], [DealNotes], [LinkedDealIds],
                [LastStageUpdatedAt], [CreatedAt])
            VALUES (?, 'USR-BROKER-01', 'Presentation', 1, 'CON-CUST01', 'CON-BROK01', 500000.00,
                'E2E test deal for brochure generation', '', GETUTCDATE(), GETUTCDATE())
        """, TENANT)
    else:
        log("  Deal already exists, updating")
        cursor.execute("""
            UPDATE [DealRecords] SET LinkedPropertyId = 1, DealStage = 'Presentation',
                [BuyerClientId] = 'CON-CUST01', [LastStageUpdatedAt] = GETUTCDATE()
            WHERE DealId = ?
        """, DEAL_ID)

    # Check if properties exist
    cursor.execute("SELECT COUNT(*) FROM [PropertyAssets] WHERE PropertyId IN (1, 2)")
    prop_count = cursor.fetchone()[0]

    if prop_count < 2:
        log("  Seeding PropertyAssets - inserting via CRM schema")
        # We need to insert into Property.Microservice DB, not CRM
        # Use the property DB
        conn_prop = pyodbc.connect(
            "DRIVER={ODBC Driver 17 for SQL Server};"
            "SERVER=localhost,1433;"
            "DATABASE=RealEstatePropertyDB;"
            "UID=sa;"
            "PWD=SecureRealEstatePass123!;"
            "TrustServerCertificate=yes;"
        )
        cur_p = conn_prop.cursor()

        # Check property type
        cur_p.execute("SELECT TOP 1 PropertyTypeId FROM PropertyTypes")
        row = cur_p.fetchone()
        prop_type_id = row.PropertyTypeId if row else 1

        # Insert Property 1 (Sale)
        cur_p.execute("""
            INSERT INTO [PropertyAssets] (
                [TenantId], [CompleteAddress], [ErfSpatialSize], [TitleDeedType],
                [PortfolioZoning], [MarketClass], [PropertyTypeId], [ValuationPrice],
                [SalePrice], [TenantInstallationAllowance], [Latitude], [Longitude],
                [VerificationDocumentsJson], [GalleryImageJson], [LinkedOwnerId],
                [PropertyDescription], [IsForSale], [IsForRent], [CreatedAt],
                [PropertyTypeDetails_PropertyTypeId]
            ) VALUES (?, '123 Cape Town Boulevard, Cape Town', '500 m²', 'Freehold',
                'Commercial Zone', 'Sale', ?, 850000.00, 1200000.00, 25000.00,
                -33.9249, 18.4241,
                '[{"name":"building-exterior.jpg","description":"Building exterior","uploadedAt":"2026-01-01T00:00:00Z"},{"name":"lobby.jpg","description":"Main lobby","uploadedAt":"2026-01-01T00:00:00Z"},{"name":"floor-plan.jpg","description":"Floor plan","uploadedAt":"2026-01-01T00:00:00Z"}]',
                '[]', 'OT-0001',
                'Premium commercial property in central Cape Town', 1, 0, GETUTCDATE(), ?
            )
        """, TENANT, prop_type_id, prop_type_id)
        cur_p.execute("SELECT SCOPE_IDENTITY()")
        prop1_id = int(cur_p.fetchone()[0])

        # Insert Property 2 (Rental)
        cur_p.execute("""
            INSERT INTO [PropertyAssets] (
                [TenantId], [CompleteAddress], [ErfSpatialSize], [TitleDeedType],
                [PortfolioZoning], [MarketClass], [PropertyTypeId], [ValuationPrice],
                [SalePrice], [TenantInstallationAllowance], [Latitude], [Longitude],
                [VerificationDocumentsJson], [GalleryImageJson], [LinkedOwnerId],
                [PropertyDescription], [IsForSale], [IsForRent], [CreatedAt],
                [PropertyTypeDetails_PropertyTypeId]
            ) VALUES (?, '456 Sandton Heights, Johannesburg', '350 m²', 'Freehold',
                'Commercial Zone', 'Rental', ?, 650000.00, NULL, 15000.00,
                -26.1027, 28.0330,
                '[{"name":"exterior-front.jpg","description":"Front exterior","uploadedAt":"2026-01-01T00:00:00Z"},{"name":"interior-lobby.jpg","description":"Lobby area","uploadedAt":"2026-01-01T00:00:00Z"},{"name":"conference-room.jpg","description":"Conference room","uploadedAt":"2026-01-01T00:00:00Z"}]',
                '[]', 'OT-0001',
                'Modern office space in Sandton', 0, 1, GETUTCDATE(), ?
            )
        """, TENANT, prop_type_id, prop_type_id)
        cur_p.execute("SELECT SCOPE_IDENTITY()")
        prop2_id = int(cur_p.fetchone()[0])

        # Insert Units for Property 1 (3 units)
        for i, (unit_no, desc, area, rate, price) in enumerate([
            ("A-101", "Ground Floor Office", 150.00, 8500.00, None),
            ("A-102", "First Floor Office", 120.00, 7200.00, None),
            ("A-103", "Mezzanine Suite", 85.00, 5800.00, None),
        ]):
            cur_p.execute("""
                INSERT INTO [PropertyUnits] (
                    [PropertyId], [UnitNumber], [UnitDescription], [NetRentableAreaSqM],
                    [RentalRate], [SpecificUnitRentalPrice], [IsCurrentlyVacant],
                    [UnitImagesJson], [UnitDocumentsJson], [CreatedAt]
                ) VALUES (?, ?, ?, ?, NULL, ?, 1,
                    '[{"name":?"unit-a{idx}-img1.jpg","description":"Unit view 1","uploadedAt":"2026-01-01T00:00:00Z"},{"name":?"unit-a{idx}-img2.jpg","description":"Unit view 2","uploadedAt":"2026-01-01T00:00:00Z"},{"name":?"unit-a{idx}-img3.jpg","description":"Unit view 3","uploadedAt":"2026-01-01T00:00:00Z"}]',
                    '[]', GETUTCDATE())
            """.replace("{idx}", str(i)), prop1_id, unit_no, desc, area, rate)

        # Insert Units for Property 2 (3 units)
        for i, (unit_no, desc, area, rate) in enumerate([
            ("B-201", "Open Plan Office", 200.00, 9500.00),
            ("B-202", "Executive Suite", 95.00, 6800.00),
            ("B-203", "Storage & Workshop", 55.00, 4200.00),
        ]):
            cur_p.execute("""
                INSERT INTO [PropertyUnits] (
                    [PropertyId], [UnitNumber], [UnitDescription], [NetRentableAreaSqM],
                    [RentalRate], [SpecificUnitRentalPrice], [IsCurrentlyVacant],
                    [UnitImagesJson], [UnitDocumentsJson], [CreatedAt]
                ) VALUES (?, ?, ?, ?, ?, NULL, 1,
                    '[{"name":?"unit-b{idx}-img1.jpg","description":"Unit view 1","uploadedAt":"2026-01-01T00:00:00Z"},{"name":?"unit-b{idx}-img2.jpg","description":"Unit view 2","uploadedAt":"2026-01-01T00:00:00Z"},{"name":?"unit-b{idx}-img3.jpg","description":"Unit view 3","uploadedAt":"2026-01-01T00:00:00Z"}]',
                    '[]', GETUTCDATE())
            """.replace("{idx}", str(i)), prop2_id, unit_no, desc, area, rate)

        conn_prop.commit()
        conn_prop.close()
        log(f"  Created Property 1 (ID: {prop1_id}) with 3 units, Property 2 (ID: {prop2_id}) with 3 units")
    else:
        log("  Properties already exist")
        # Get the property IDs
        cursor.execute("SELECT TOP 2 PropertyId FROM PropertyAssets WHERE TenantId = ? ORDER BY PropertyId", TENANT)
        rows = cursor.fetchall()
        prop1_id = rows[0].PropertyId
        prop2_id = rows[1].PropertyId if len(rows) > 1 else rows[0].PropertyId

    conn.commit()
    conn.close()
except Exception as e:
    log(f"  SQL ERROR: {e}")
    import traceback
    traceback.print_exc()
    # Continue anyway - properties might already exist
    prop1_id = 1
    prop2_id = 2

# ── Step 3: Create brochure card via CRM API ──
log("Step 3: Creating brochure card via CRM API")
# First get a unit ID from property 1
r = api_get(f"{PROP_BASE}/properties/{prop1_id}/units")
print(f"  GET units from prop1: {r.status_code}")
if r.status_code == 200:
    units1 = r.json()
    unit1_id = units1[0]["PropertyUnitId"] if units1 else None
    print(f"  Unit 1: {units1[0]['UnitNumber'] if units1 else 'none'}")
else:
    print(f"  Response: {r.text[:200]}")
    unit1_id = None

r = api_get(f"{PROP_BASE}/properties/{prop2_id}/units")
print(f"  GET units from prop2: {r.status_code}")
if r.status_code == 200:
    units2 = r.json()
    unit2_id = units2[0]["PropertyUnitId"] if units2 else None
    print(f"  Unit 1: {units2[0]['UnitNumber'] if units2 else 'none'}")
else:
    print(f"  Response: {r.text[:200]}")
    unit2_id = None

# Get amenities for both properties
for pid in [prop1_id, prop2_id]:
    r = api_get(f"{PROP_BASE}/properties/{pid}/amenities")
    print(f"  GET amenities from prop {pid}: {r.status_code}")
    if r.status_code == 200:
        data = r.json()
        print(f"    Items: {len(data.get('items', []))}, IsResidential: {data.get('isResidential', 'N/A')}")

# Create brochure card - try with selected units from both properties
card_payload = {
    "dealId": DEAL_ID,
    "clientName": CLIENT_NAME,
    "clientEmail": CLIENT_EMAIL,
    "clientPhone": CLIENT_PHONE,
    "companyName": COMPANY_NAME,
    "companyLogoUrl": "",
    "units": [
        {
            "propertyId": prop1_id,
            "unitId": unit1_id,
            "unitNumber": "A-101",
            "unitDescription": "Ground Floor Office",
            "propertyDescription": "Premium commercial property in central Cape Town",
            "netRentableAreaSqM": 150.0,
            "rentalRate": 8500.0,
            "rentEscalationPercentage": 7.5,
            "leaseTermMonths": 36,
            "salePrice": None,
            "selectedImageRefs": [],
            "marketClass": "Sale"
        },
        {
            "propertyId": prop1_id,
            "unitId": unit1_id,
            "unitNumber": "A-102",
            "unitDescription": "First Floor Office",
            "propertyDescription": "Premium commercial property in central Cape Town",
            "netRentableAreaSqM": 120.0,
            "rentalRate": 7200.0,
            "rentEscalationPercentage": 7.5,
            "leaseTermMonths": 36,
            "salePrice": None,
            "selectedImageRefs": [],
            "marketClass": "Sale"
        },
        {
            "propertyId": prop2_id,
            "unitId": unit2_id,
            "unitNumber": "B-201",
            "unitDescription": "Open Plan Office",
            "propertyDescription": "Modern office space in Sandton",
            "netRentableAreaSqM": 200.0,
            "rentalRate": 9500.0,
            "rentEscalationPercentage": 6.0,
            "leaseTermMonths": 24,
            "salePrice": None,
            "selectedImageRefs": [],
            "marketClass": "Rental"
        }
    ]
}
r = api_post(f"{GATEWAY}/crm/brochure-cards", card_payload)
print(f"  POST brochure card: {r.status_code}")
if r.status_code in (200, 201):
    card_data = r.json()
    print(f"  Card created: CardId={card_data.get('cardId')}")
else:
    print(f"  Response: {r.text[:500]}")

# Try the upsert endpoint instead
r = api_post(f"{GATEWAY}/crm/brochure-cards", {
    "dealId": DEAL_ID,
    "clientName": CLIENT_NAME,
    "clientEmail": CLIENT_EMAIL,
    "clientPhone": CLIENT_PHONE,
    "companyName": COMPANY_NAME,
    "units": []
})
print(f"  POST brochure-cards (upsert): {r.status_code}")
if r.status_code in (200, 201):
    card_data = r.json()
    print(f"  Card response: {card_data}")
else:
    print(f"  Response: {r.text[:500]}")

# ── Step 4: Try to trigger brochure generation via the property API ──
log("Step 4: Triggering brochure generation")
payload = {
    "DealId": DEAL_ID,
    "BrochureCardId": 1,
    "PropertyId": prop1_id,
    "TenantId": TENANT,
    "RequestedBy": "USR-BROKER-01",
    "BrokerName": BROKER_NAME,
    "BrokerEmail": BROKER_EMAIL_ADDR,
    "BrokerPhone": BROKER_PHONE,
    "CompanyName": COMPANY_NAME,
    "CompanyLogoUrl": "",
    "ClientName": CLIENT_NAME,
    "ClientEmail": CLIENT_EMAIL,
    "ClientPhone": CLIENT_PHONE,
    "MarketClass": "Both",
    "TenantInstallationAllowance": 25000.00,
    "SubUnits": [
        {
            "PropertyId": prop1_id,
            "PropertyDescription": "Premium commercial property in central Cape Town",
            "Latitude": -33.9249,
            "Longitude": 18.4241,
            "UnitNumber": "A-101",
            "UnitDescription": "Ground Floor Office",
            "NetRentableAreaSqM": 150.0,
            "RentalRate": 8500.0,
            "RentEscalationPercentage": 7.5,
            "LeaseTermMonths": 36,
            "SalePrice": None,
            "SelectedImageRefs": [],
            "PropertyType": "Commercial",
            "ValuationPrice": 850000.00,
            "ErfSpatialSize": "500 m²",
            "TitleDeedType": "Freehold",
            "PortfolioZoning": "Commercial Zone",
            "IsForSale": True,
            "IsForRent": False,
            "IsCurrentlyVacant": True,
            "Amenities": [
                {"Name": "Parking", "Label": "Allocated parking bays", "BooleanValue": True, "NumberValue": 4},
                {"Name": "Security", "Label": "24/7 security", "BooleanValue": True, "NumberValue": None},
                {"Name": "AirConditioning", "Label": "Air conditioning", "BooleanValue": True, "NumberValue": None}
            ]
        },
        {
            "PropertyId": prop1_id,
            "PropertyDescription": "Premium commercial property in central Cape Town",
            "Latitude": -33.9249,
            "Longitude": 18.4241,
            "UnitNumber": "A-102",
            "UnitDescription": "First Floor Office",
            "NetRentableAreaSqM": 120.0,
            "RentalRate": 7200.0,
            "RentEscalationPercentage": 7.5,
            "LeaseTermMonths": 36,
            "SalePrice": None,
            "SelectedImageRefs": [],
            "PropertyType": "Commercial",
            "ValuationPrice": 850000.00,
            "ErfSpatialSize": "500 m²",
            "TitleDeedType": "Freehold",
            "PortfolioZoning": "Commercial Zone",
            "IsForSale": True,
            "IsForRent": False,
            "IsCurrentlyVacant": True,
            "Amenities": [
                {"Name": "Parking", "Label": "Allocated parking bays", "BooleanValue": True, "NumberValue": 4},
                {"Name": "Security", "Label": "24/7 security", "BooleanValue": True, "NumberValue": None},
                {"Name": "SwimmingPool", "Label": "Swimming pool", "BooleanValue": False, "NumberValue": None}
            ]
        },
        {
            "PropertyId": prop2_id,
            "PropertyDescription": "Modern office space in Sandton",
            "Latitude": -26.1027,
            "Longitude": 28.0330,
            "UnitNumber": "B-201",
            "UnitDescription": "Open Plan Office",
            "NetRentableAreaSqM": 200.0,
            "RentalRate": 9500.0,
            "RentEscalationPercentage": 6.0,
            "LeaseTermMonths": 24,
            "SalePrice": None,
            "SelectedImageRefs": [],
            "PropertyType": "Commercial",
            "ValuationPrice": 650000.00,
            "ErfSpatialSize": "350 m²",
            "TitleDeedType": "Freehold",
            "PortfolioZoning": "Commercial Zone",
            "IsForSale": False,
            "IsForRent": True,
            "IsCurrentlyVacant": True,
            "Amenities": [
                {"Name": "Parking", "Label": "Allocated parking bays", "BooleanValue": True, "NumberValue": 3},
                {"Name": "Security", "Label": "24/7 security", "BooleanValue": True, "NumberValue": None},
                {"Name": "Gym", "Label": "Gym", "BooleanValue": True, "NumberValue": None}
            ]
        }
    ]
}

r = api_post(f"{PROP_BASE}/properties/brochure/generate", payload)
print(f"  POST brochure generate: {r.status_code}")
print(f"  Response: {r.text[:500]}")
if r.status_code == 202:
    log("  Brochure generation queued successfully!")
else:
    log(f"  ERROR: Brochure generation failed with status {r.status_code}")

# ── Step 5: Wait for PDF generation ──
log("Step 5: Waiting for PDF generation (up to 30 seconds)")
pdf_found = False
for i in range(30):
    time.sleep(1)
    if os.path.exists(GENERATED_BROCHURES_DIR):
        files = os.listdir(GENERATED_BROCHURES_DIR)
        pdfs = [f for f in files if f.endswith('.pdf')]
        if pdfs:
            print(f"  Found PDF after {i+1}s: {pdfs}")
            pdf_found = True
            break
    print(f"  Waiting... ({i+1}s elapsed)", end="\r", flush=True)

if not pdf_found:
    log("  No PDF found in GeneratedBrochures directory")
    # Check alternative locations
    for root, dirs, files in os.walk(os.path.join(os.path.dirname(os.path.abspath(__file__)), "backend-services")):
        for f in files:
            if f.endswith('.pdf'):
                print(f"  Found PDF at: {os.path.join(root, f)}")

# ── Step 6: Check brochure card status ──
log("Step 6: Checking brochure card status")
r = api_get(f"{GATEWAY}/crm/brochure-cards", {"dealId": DEAL_ID})
print(f"  GET brochure cards: {r.status_code}")
if r.status_code == 200:
    cards = r.json()
    print(f"  Cards: {json.dumps(cards, indent=2)[:1000]}")
else:
    print(f"  Response: {r.text[:500]}")

# ── Step 7: Check notification in CRM database ──
log("Step 7: Checking notification in CRM database")
try:
    conn = pyodbc.connect(conn_str)
    cursor = conn.cursor()
    cursor.execute("""
        SELECT TOP 1 [Title], [Body], [Category], [CreatedAt]
        FROM [Notifications]
        WHERE [TenantId] = ?
        ORDER BY [CreatedAt] DESC
    """, TENANT)
    row = cursor.fetchone()
    if row:
        print(f"  Notification Title: {row.Title}")
        print(f"  Notification Body: {row.Body}")
        print(f"  Category: {row.Category}")
        print(f"  CreatedAt: {row.CreatedAt}")
    else:
        print("  No notifications found in CRM DB")

    cursor.execute("""
        SELECT TOP 1 [CardId], [Status], [GeneratedFilePath], [UpdatedAt]
        FROM [BrochureCards]
        WHERE [DealId] = ?
        ORDER BY [UpdatedAt] DESC
    """, DEAL_ID)
    row = cursor.fetchone()
    if row:
        print(f"  Card Status: {row.Status}")
        print(f"  GeneratedFilePath: {row.GeneratedFilePath}")
        print(f"  UpdatedAt: {row.UpdatedAt}")
    conn.close()
except Exception as e:
    print(f"  DB Error: {e}")

log("E2E test complete!")