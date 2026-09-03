#!/usr/bin/env bash
# Tear down all deployed Google Cloud resources for Real Estate Enterprise.
# Use with caution — this is irreversible.

set -euo pipefail
PROJECT_ID="${PROJECT_ID:-real-estate-enterprise-prod}"
REGION="${REGION:-us-central1}"
REPO_NAME="realestate-images"
SQL_INSTANCE="realestate-sql"

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log()   { echo -e "${GREEN}[+]${NC} $*"; }
warn()  { echo -e "${YELLOW}[!]${NC} $*"; }

gcloud config set project "${PROJECT_ID}" --quiet

read -p "Delete ALL resources in ${PROJECT_ID}? Type 'destroy' to confirm: " confirm
[ "$confirm" = "destroy" ] || { echo "Aborted."; exit 1; }

log "Deleting Cloud Run services..."
for svc in identity-service property-service crm-service developer-api gateway frontend-client client-facing brochure-worker image-worker syndication-worker; do
  gcloud run services delete "${svc}" --region="${REGION}" --quiet 2>/dev/null || warn "${svc} not found"
done

log "Deleting domain mappings..."
for domain in app www api; do
  for svc in frontend-client client-facing gateway; do
    gcloud run domain-mappings delete --domain="${domain}.${DOMAIN:-yourdomain.com}" --service="${svc}" --region="${REGION}" --quiet 2>/dev/null || true
  done
done

log "Deleting Cloud SQL instance..."
gcloud sql instances delete "${SQL_INSTANCE}" --quiet 2>/dev/null || warn "SQL instance not found"

log "Deleting VPC connector..."
gcloud compute networks vpc-access connectors delete realestate-connector --region="${REGION}" --quiet 2>/dev/null || warn "VPC connector not found"

log "Deleting secrets..."
for secret in jwt-secret mssql-password support-email; do
  gcloud secrets delete "${secret}" --quiet 2>/dev/null || warn "${secret} not found"
done

log "Deleting service accounts..."
for sa in identity property crm developer-api gateway frontend-client client-facing brochure-worker image-worker syndication-worker monitoring; do
  gcloud iam service-accounts delete "${sa}-sa@${PROJECT_ID}.iam.gserviceaccount.com" --quiet 2>/dev/null || true
done

log "Deleting Artifact Registry repository..."
gcloud artifacts repositories delete "${REPO_NAME}" --location="${REGION}" --quiet 2>/dev/null || warn "Repository not found"

log "Done."
