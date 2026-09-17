#!/usr/bin/env bash
# Tear down regional Google Cloud resources created by deploy.sh.
# Shared secrets (jwt-secret, mssql-password) are kept so other regions keep working.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
REPO_NAME="${REPO_NAME:-realestate-images}"
SQL_INSTANCE="${SQL_INSTANCE:-realestate-sql-${REGION}}"
DOMAIN="${DOMAIN:-biscovetech.com}"

if [ -z "${PROJECT_ID}" ]; then
  error "Set PROJECT_ID"
fi

gcloud config set project "${PROJECT_ID}" --quiet

read -r -p "Delete Cloud Run/SQL/Artifact Registry in ${PROJECT_ID} ${REGION}? Type 'destroy' to confirm: " confirm
[ "${confirm}" = "destroy" ] || { echo "Aborted."; exit 1; }

log "Deleting Cloud Run services in ${REGION}..."
for svc in identity-service property-service crm-service developer-api gateway frontend-client client-facing; do
  gcloud run services delete "${svc}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || warn "${svc} not found"
done

if [ -n "${DOMAIN}" ]; then
  log "Deleting domain mappings..."
  gcloud beta run domain-mappings delete --domain="app.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || true
  gcloud beta run domain-mappings delete --domain="www.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || true
  gcloud beta run domain-mappings delete --domain="api.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || true
fi

log "Deleting Cloud SQL instance ${SQL_INSTANCE}..."
gcloud sql instances delete "${SQL_INSTANCE}" --project="${PROJECT_ID}" --quiet 2>/dev/null || warn "SQL instance not found"

log "Deleting regional connection secrets..."
for secret in \
  "identity-service-db-conn-${REGION}" \
  "property-service-db-conn-${REGION}" \
  "crm-service-db-conn-${REGION}" \
  "developer-api-db-conn-${REGION}" \
  "developer-api-property-db-conn-${REGION}"; do
  gcloud secrets delete "${secret}" --project="${PROJECT_ID}" --quiet 2>/dev/null || warn "${secret} not found"
done

log "Deleting Artifact Registry repository in ${REGION}..."
gcloud artifacts repositories delete "${REPO_NAME}" --location="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || warn "Repository not found"

log "Done. Shared jwt-secret and mssql-password were not deleted."
