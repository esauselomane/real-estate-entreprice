#!/usr/bin/env bash
set -euo pipefail

# Provisions a Global External HTTP(S) Load Balancer for Cloud Run services.
# This is required for regions like africa-south1 which do not support legacy domain mappings.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
DOMAIN="${DOMAIN:-biscovetech.com}"
LB_NAME="realestate-lb"
IP_NAME="realestate-ip"
CERT_NAME="realestate-cert"

require_command gcloud

if [ -z "${PROJECT_ID}" ]; then
  error "Set PROJECT_ID before running deploy-load-balancer.sh"
fi

gcloud config set project "${PROJECT_ID}" --quiet

log "Step 1: Reserving a Global Static IP..."
gcloud compute addresses create "${IP_NAME}" --global --quiet 2>/dev/null || log "IP ${IP_NAME} already exists."
STATIC_IP=$(gcloud compute addresses describe "${IP_NAME}" --global --format='value(address)')
log "Load Balancer Static IP: ${STATIC_IP}"

log "Step 2: Creating Serverless NEGs for each service..."
for svc in frontend-client gateway client-facing; do
  gcloud compute network-endpoint-groups create "${svc}-neg" \
    --region="${REGION}" \
    --network-endpoint-type=serverless \
    --cloud-run-service="${svc}" --quiet 2>/dev/null || log "NEG ${svc}-neg already exists."
done

log "Step 3: Creating Backend Services..."
for svc in frontend-client gateway client-facing; do
  gcloud compute backend-services create "${svc}-backend" --global --quiet 2>/dev/null || log "Backend ${svc}-backend already exists."
  
  log "Ensuring NEG is attached to backend service for ${svc}..."
  gcloud compute backend-services add-backend "${svc}-backend" \
    --global \
    --network-endpoint-group="${svc}-neg" \
    --network-endpoint-group-region="${REGION}" --quiet 2>/dev/null || log "NEG already attached to ${svc}."
done

log "Step 4: Creating Managed SSL Certificate for ${DOMAIN}..."
gcloud compute ssl-certificates create "${CERT_NAME}" \
  --domains="${DOMAIN},app.${DOMAIN},api.${DOMAIN},www.${DOMAIN}" \
  --global --quiet 2>/dev/null || log "SSL Cert ${CERT_NAME} already exists."

log "Step 5: Creating and configuring URL Map..."
MAP_FILE="$(mktemp).yaml"
cat <<EOF > "${MAP_FILE}"
name: ${LB_NAME}-url-map
defaultService: https://www.googleapis.com/compute/v1/projects/${PROJECT_ID}/global/backendServices/client-facing-backend
hostRules:
- hosts:
  - "app.${DOMAIN}"
  pathMatcher: app-matcher
- hosts:
  - "api.${DOMAIN}"
  pathMatcher: api-matcher
- hosts:
  - "www.${DOMAIN}"
  - "${DOMAIN}"
  pathMatcher: www-matcher
pathMatchers:
- defaultService: https://www.googleapis.com/compute/v1/projects/${PROJECT_ID}/global/backendServices/frontend-client-backend
  name: app-matcher
- defaultService: https://www.googleapis.com/compute/v1/projects/${PROJECT_ID}/global/backendServices/gateway-backend
  name: api-matcher
- defaultService: https://www.googleapis.com/compute/v1/projects/${PROJECT_ID}/global/backendServices/client-facing-backend
  name: www-matcher
EOF

if gcloud compute url-maps describe "${LB_NAME}-url-map" --global >/dev/null 2>&1; then
  gcloud compute url-maps import "${LB_NAME}-url-map" --source="${MAP_FILE}" --global --quiet
else
  gcloud compute url-maps create "${LB_NAME}-url-map" --default-service="client-facing-backend" --global --quiet
  gcloud compute url-maps import "${LB_NAME}-url-map" --source="${MAP_FILE}" --global --quiet
fi
rm -f "${MAP_FILE}"

log "Step 6: Creating Target HTTPS Proxy..."
gcloud compute target-https-proxies create "${LB_NAME}-https-proxy" \
  --url-map="${LB_NAME}-url-map" \
  --ssl-certificates="${CERT_NAME}" --quiet 2>/dev/null || log "Proxy already exists."

log "Step 7: Creating Global Forwarding Rule..."
gcloud compute forwarding-rules create "${LB_NAME}-https-rule" \
  --address="${IP_NAME}" \
  --target-https-proxy="${LB_NAME}-https-proxy" \
  --global \
  --ports=443 --quiet 2>/dev/null || log "Forwarding rule already exists."

log "Step 8: Creating HTTP to HTTPS Redirect Map..."
REDIRECT_MAP_FILE="$(mktemp).yaml"
cat <<EOF > "${REDIRECT_MAP_FILE}"
kind: compute#urlMap
name: ${LB_NAME}-redirect-map
defaultUrlRedirect:
  httpsRedirect: true
  redirectResponseCode: MOVED_PERMANENTLY_DEFAULT
EOF

gcloud compute url-maps import "${LB_NAME}-redirect-map" \
  --source="${REDIRECT_MAP_FILE}" --global --quiet 2>/dev/null || log "Redirect map already exists."
rm -f "${REDIRECT_MAP_FILE}"

log "Step 9: Creating Target HTTP Proxy for redirect..."
gcloud compute target-http-proxies create "${LB_NAME}-http-proxy" \
  --url-map="${LB_NAME}-redirect-map" --global --quiet 2>/dev/null || log "HTTP proxy already exists."

log "Step 10: Creating HTTP Forwarding Rule (Port 80)..."
gcloud compute forwarding-rules create "${LB_NAME}-http-rule" \
  --address="${IP_NAME}" \
  --target-http-proxy="${LB_NAME}-http-proxy" \
  --global \
  --ports=80 --quiet 2>/dev/null || log "HTTP forwarding rule already exists."

log "========================================================"
log "LOAD BALANCER SETUP COMPLETE"
log "1. Update your DNS (GoDaddy/Cloudflare) A records for:"
log "   - app.${DOMAIN}"
log "   - api.${DOMAIN}"
log "   - www.${DOMAIN}"
log "   To point to the Static IP: ${STATIC_IP}"
log "2. Managed SSL certificates take 30-60 minutes to provision."
log "========================================================"