#!/bin/bash

echo "============================================================"
echo "  END-TO-END BROCHURE GENERATION TEST"
echo "============================================================"
echo ""

qnum() {
    docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C "$@" 2>/dev/null | awk 'NR==1{gsub(/[[:space:]]/,""); print}'
}

qrow() {
    docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C "$@" 2>/dev/null | tail -n +3 | head -5
}

# ── PHASE 0: Database Connectivity ──
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 0: Database Connectivity"
echo "─────────────────────────────────────────────────────────────"

echo -n "[CRM DB]  Tables: "; qnum -d RealEstateCRMDB -Q "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES"; echo ""
echo -n "[CRM DB]  BrochureCards: "; qnum -d RealEstateCRMDB -Q "SELECT COUNT(*) FROM BrochureCards"; echo " rows"
echo -n "[CRM DB]  BrochureCardLines: "; qnum -d RealEstateCRMDB -Q "SELECT COUNT(*) FROM BrochureCardLines"; echo " rows"
echo -n "[Prop DB] PropertyAssets: "; qnum -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM PropertyAssets"; echo " rows"
echo -n "[Prop DB] PropertyUnits: "; qnum -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM PropertyUnits"; echo " rows"
echo -n "[Prop DB] AmenityTypes: "; qnum -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM AmenityTypes"; echo " types"
echo -n "[Prop DB] PropertyUnitAmenities: "; qnum -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM PropertyUnitAmenities"; echo " mappings"

# ── PHASE 1: Schema Verification ──
echo ""
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 1: Database Schema — New Column Verification"
echo "─────────────────────────────────────────────────────────────"

P1_PASS=0; P1_FAIL=0
echo ""
echo "[PropertyAssets] New columns:"
for col in IsForSale IsForRent ValuationPrice ErfSpatialSize TitleDeedType PortfolioZoning TenantInstallationAllowance; do
    COUNT=$(docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PropertyAssets' AND COLUMN_NAME='$col'" -h -1 2>/dev/null | awk 'NR==1{gsub(/[[:space:]]/,""); print}')
    if [ "$COUNT" = "1" ]; then
        echo "  ✓ $col"; P1_PASS=$((P1_PASS+1))
    else
        echo "  ✗ $col (got: $COUNT)"; P1_FAIL=$((P1_FAIL+1))
    fi
done

echo ""
echo "[PropertyUnits] Key columns:"
for col in IsCurrentlyVacant RentalRate RentEscalationPercentage LeaseTermMonths UnitImagesJson UnitDocumentsJson; do
    COUNT=$(docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C -d RealEstatePropertyDB -Q "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PropertyUnits' AND COLUMN_NAME='$col'" -h -1 2>/dev/null | awk 'NR==1{gsub(/[[:space:]]/,""); print}')
    if [ "$COUNT" = "1" ]; then
        echo "  ✓ $col"; P1_PASS=$((P1_PASS+1))
    else
        echo "  ✗ $col (got: $COUNT)"; P1_FAIL=$((P1_FAIL+1))
    fi
done

echo ""
echo "[PropertyAssets] Data sample:"
qrow -d RealEstatePropertyDB -Q "SELECT TOP 3 PropertyId, CompleteAddress, ValuationPrice, IsForSale, IsForRent, ErfSpatialSize, TitleDeedType, PortfolioZoning, TenantInstallationAllowance FROM PropertyAssets" | sed 's/^/  /'

echo ""
echo "[PropertyUnits] Data sample:"
qrow -d RealEstatePropertyDB -Q "SELECT TOP 3 PropertyUnitId, PropertyId, UnitNumber, IsCurrentlyVacant, RentalRate FROM PropertyUnits" | sed 's/^/  /'

echo ""
echo "[AmenityTypes] Available:"
qrow -d RealEstatePropertyDB -Q "SELECT AmenityTypeId, Name FROM AmenityTypes" | sed 's/^/  /'

# ── PHASE 2: Template Verification ──
echo ""
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 2: Template Content Verification"
echo "─────────────────────────────────────────────────────────────"

TEMPLATE_DIR="/Users/uboomi/Desktop/real-estate-entreprice/templates"
TP=0; TF=0

check_tpl() {
    local tpl="$1"; shift
    local label="$1"; shift
    if [ ! -f "$tpl" ]; then
        echo "  ✗ $label: file missing"; TF=$((TF+1)); return
    fi
    for c in "$@"; do
        if grep -qi "$c" "$tpl" 2>/dev/null; then
            echo "  ✓ $label: '$c'"; TP=$((TP+1))
        else
            echo "  ✗ $label: missing '$c'"; TF=$((TF+1))
        fi
    done
}

echo ""
echo "[Cover Page]"
check_tpl "$TEMPLATE_DIR/brochure/partials/cover.html" "Cover" \
    "CONFIDENTIAL PROPOSAL" "linear-gradient" "Segoe UI" "36px" "Prepared By" "Prepared For"

echo ""
echo "[Unit Page]"
check_tpl "$TEMPLATE_DIR/brochure/partials/unit.html" "Unit" \
    "SALE DETAILS" "RENTAL TERMS" "AVAILABLE" "OCCUPIED" \
    "border-left" "IconSvg" "SelectedImageRefs" \
    "PropertyType" "ValuationPrice" "ErfSpatialSize" "TitleDeedType" \
    "TenantInstallationAllowance" "isRental" "isCurrentlyVacant" "EstimatedBondRepayment"

echo ""
echo "[Map Page]"
check_tpl "$TEMPLATE_DIR/brochure/partials/map-page.html" "Map" \
    "2563EB" "border-bottom" "leaflet" "Latitude" "Longitude" "UnitNumber" "subUnits"

echo ""
echo "[Document Layout]"
check_tpl "$TEMPLATE_DIR/brochure/document.html" "Document" \
    "@page" "bottom-center" "margin" "coverHtml" "mapPageHtml" "unitPagesHtml"

echo ""
echo "[Notification]"
check_tpl "$TEMPLATE_DIR/notification/brochure-generated.html" "Notification" \
    "BROCHURE" "Download" "broker_name" "deal_address" "unit_count" "cta-button" "success-icon"

# ── PHASE 3: Code Consistency ──
echo ""
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 3: Code Consistency Verification"
echo "─────────────────────────────────────────────────────────────"

CP=0; CF=0

check_code() {
    local desc="$1"; shift
    local pattern="$1"; shift
    local found=false
    for f in "$@"; do
        if [ -f "$f" ] && grep -q "$pattern" "$f" 2>/dev/null; then
            found=true; break
        fi
    done
    if $found; then
        echo "  ✓ $desc"; CP=$((CP+1))
    else
        echo "  ✗ $desc — '$pattern' not found"; CF=$((CF+1))
    fi
}

echo ""
echo "[DTOs — BrochureSubUnit]"
check_code "PropertyType" "PropertyType" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "ValuationPrice" "ValuationPrice" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "ErfSpatialSize" "ErfSpatialSize" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "TitleDeedType" "TitleDeedType" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "PortfolioZoning" "PortfolioZoning" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "IsForSale" "IsForSale" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "IsForRent" "IsForRent" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "IsCurrentlyVacant" "IsCurrentlyVacant" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "Amenities" "Amenities" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"
check_code "AmenityInfo nested" "AmenityInfo" "backend-services/Property.Microservice/DTOs/BrochureSubUnit.cs"

echo ""
echo "[DTOs — CRM Request]"
check_code "PropertyType" "PropertyType" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "ValuationPrice" "ValuationPrice" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "IsForSale" "IsForSale" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "IsForRent" "IsForRent" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "IsCurrentlyVacant" "IsCurrentlyVacant" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "Amenities" "Amenities" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"
check_code "AmenityInfo class" "AmenityInfo" "backend-services/CRM.Microservice/DTOs/BrochureGenerationRequest.cs"

echo ""
echo "[JobDefinitions]"
check_code "PropertyType" "PropertyType" "backend-services/CRM.Microservice/Services/BackgroundJobs/JobDefinitions.cs"
check_code "Amenities" "Amenities" "backend-services/CRM.Microservice/Services/BackgroundJobs/JobDefinitions.cs"
check_code "AmenityInfo record" "record AmenityInfo" "backend-services/CRM.Microservice/Services/BackgroundJobs/JobDefinitions.cs"

echo ""
echo "[CRM Controller]"
check_code "PropertyType mapping" "PropertyType" "backend-services/CRM.Microservice/Controllers/CRMController.cs"
check_code "Amenities mapping" "Amenities" "backend-services/CRM.Microservice/Controllers/CRMController.cs"
check_code "IsForSale mapping" "IsForSale" "backend-services/CRM.Microservice/Controllers/CRMController.cs"

echo ""
echo "[Property Assets Controller]"
check_code "New fields" "ValuationPrice" "backend-services/Property.Microservice/Controllers/PropertyAssetsController.cs"
check_code "Amenities" "Amenities" "backend-services/Property.Microservice/Controllers/PropertyAssetsController.cs"
check_code "IsForSale" "IsForSale" "backend-services/Property.Microservice/Controllers/PropertyAssetsController.cs"

echo ""
echo "[Background Service]"
check_code "ProcessJob maps PropertyType" "PropertyType" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "ProcessJob maps Amenities" "Amenities" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml IsForSale" "IsForSale" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml IsForRent" "IsForRent" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml vacancy badge" "Available\|Occupied\|Vacant" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml amenity grid" "Amenit" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml SVG" "svg\|Svg" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml footer" "bottom-center" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml border-left" "border-left" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "BuildHtml blue accent" "2563EB" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"
check_code "Private AmenityInfo" "private.*AmenityInfo\|record AmenityInfo" "backend-services/CRM.Microservice/Services/BackgroundJobs/BrochureGenerationBackgroundService.cs"

echo ""
echo "[Frontend]"
check_code "ValuationPrice" "ValuationPrice" "frontend-client/src/components/BrochureBuilderWorkspace.jsx"
check_code "getPropertyAmenities" "getPropertyAmenities" "frontend-client/src/components/BrochureBuilderWorkspace.jsx"
check_code "amenities payload" "amenities" "frontend-client/src/components/BrochureBuilderWorkspace.jsx"
check_code "propertyService" "getPropertyAmenities" "frontend-client/src/services/propertyService.js"

# ── PHASE 4: Database Write Test ──
echo ""
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 4: Database Write Test"
echo "─────────────────────────────────────────────────────────────"

echo ""
echo "[TEST] Insert BrochureCard..."
CARD_ID=$(docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C -d RealEstateCRMDB -Q "SET NOCOUNT ON; INSERT INTO [BrochureCards] ([TenantId],[DealId],[BrokerAgentId],[ClientName],[ClientEmail],[ClientPhone],[Status],[IsDeleted],[GeneratedFilePath]) VALUES ('TEN-0001', 1006, 'BRK-0001', 'Test Client', 'test@example.com', '+27123456789', 'Active', 0, '/brochures/test.pdf'); SELECT SCOPE_IDENTITY();" 2>/dev/null | awk 'NR==1{gsub(/[[:space:]]/,""); print}')
echo "  CardId: $CARD_ID"

echo ""
echo "[TEST] Insert BrochureCardLines..."
docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C -d RealEstateCRMDB -Q "SET NOCOUNT ON; INSERT INTO [BrochureCardLines] ([CardId],[PropertyId],[UnitId],[UnitNumber],[UnitDescription],[NetRentableAreaSqM],[ImageRef],[IsDeleted]) VALUES ($CARD_ID, 1, 1, '1A', 'Master Suite', 45.5, '[\"img1.jpg\"]', 0); INSERT INTO [BrochureCardLines] ([CardId],[PropertyId],[UnitId],[UnitNumber],[UnitDescription],[NetRentableAreaSqM],[ImageRef],[IsDeleted]) VALUES ($CARD_ID, 2, 2, '2B', 'Ocean View', 38.0, '[\"img2.jpg\"]', 0); SELECT @@ROWCOUNT;" 2>&1 | tail -5

echo ""
echo "[TEST] Verify BrochureCard..."
qrow -d RealEstateCRMDB -Q "SELECT [CardId],[ClientName],[Status],[GeneratedFilePath] FROM [BrochureCards] WHERE [CardId] = $CARD_ID" | sed 's/^/  /'

echo ""
echo "[TEST] Verify BrochureCardLines..."
qrow -d RealEstateCRMDB -Q "SELECT [LineId],[CardId],[UnitNumber],[PropertyId],[UnitDescription],[NetRentableAreaSqM] FROM [BrochureCardLines] WHERE [CardId] = $CARD_ID" | sed 's/^/  /'

echo ""
echo "[TEST] PropertyAssets - IsCurrentlyVacant check (should be in PropertyUnits):"
docker exec realestate-sqlserver-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SecureRealEstatePass123!' -C -d RealEstatePropertyDB -Q "SELECT TOP 2 PropertyUnitId, PropertyId, UnitNumber, IsCurrentlyVacant, RentalRate FROM PropertyUnits;" 2>&1 | tail -5

# ── PHASE 5: Template Parsing ──
echo ""
echo "─────────────────────────────────────────────────────────────"
echo "PHASE 5: Template Rendering Analysis"
echo "─────────────────────────────────────────────────────────────"

echo ""
echo "[Handlebars placeholders per template]"
for f in "$TEMPLATE_DIR/brochure/partials/"*.html "$TEMPLATE_DIR/brochure/document.html" "$TEMPLATE_DIR/notification/"*.html; do
    NAME=$(basename "$f")
    PLACEHOLDERS=$(grep -oE '\{\{[^}]+\}\}' "$f" 2>/dev/null | sort -u | wc -l)
    CONDITIONALS=$(grep -oE '#if [a-zA-Z]+' "$f" 2>/dev/null | sort -u | tr '\n' ' ')
    EACH=$(grep -oE '#each [a-zA-Z]+' "$f" 2>/dev/null | sort -u | tr '\n' ' ')
    echo "  $NAME: $PLACEHOLDERS placeholders | if: $CONDITIONALS | each: $EACH"
done

# ── SUMMARY ──
echo ""
echo "============================================================"
echo "  TEST SUMMARY"
echo "============================================================"
echo "  Schema checks:      $P1_PASS passed, $P1_FAIL failed"
echo "  Template checks:    $TP passed, $TF failed"
echo "  Code consistency:   $CP passed, $CF failed"
echo "============================================================"

TOTAL_FAIL=$((P1_FAIL + TF + CF))
if [ "$TOTAL_FAIL" -eq 0 ]; then
    echo "  ALL TESTS PASSED ✓"
else
    echo "  $TOTAL_FAIL CHECK(S) FAILED — review above"
fi
