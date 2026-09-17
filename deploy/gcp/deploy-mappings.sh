#!/usr/bin/env bash
set -euo pipefail

# Provision and refresh domain mappings for Cloud Run services.
# This script deletes existing mappings first to ensure a clean state.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
DOMAIN="${DOMAIN:-biscovetech.com}"

require_command gcloud

if [ -z "${PROJECT_ID}" ]; then
  error "Set PROJECT_ID before running deploy-mappings.sh"
fi

if [ -z "${DOMAIN}" ]; then
  error "Set DOMAIN before running deploy-mappings.sh. Mappings cannot be created without a base domain."
fi

gcloud config set project "${PROJECT_ID}" --quiet

log "Deleting existing domain mappings for ${DOMAIN} in ${REGION}..."
for sub in app api www; do
  gcloud beta run domain-mappings delete --domain="${sub}.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || true
done

log "Creating new domain mappings..."
gcloud beta run domain-mappings create --service=frontend-client --domain="app.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet
gcloud beta run domain-mappings create --service=gateway --domain="api.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet
gcloud beta run domain-mappings create --service=client-facing --domain="www.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet

log "Domain mapping process complete. Wait ~15 minutes for SSL certificates to provision."