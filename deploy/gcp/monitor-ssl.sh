#!/usr/bin/env bash
# monitor-ssl.sh: Periodically check the provisioning status of the Load Balancer SSL certificate.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

CERT_NAME="realestate-cert"

if [ -z "${PROJECT_ID:-}" ]; then
  error "PROJECT_ID environment variable is not set."
fi

log "Monitoring SSL Certificate: ${CERT_NAME}..."
log "This process usually takes 30-60 minutes. Press Ctrl+C to stop."
log "--------------------------------------------------------"

while true; do
  # Get the overall status and individual domain statuses
  DATA=$(gcloud compute ssl-certificates describe "${CERT_NAME}" --global --format="json(managed.status,managed.domainStatus)")
  STATUS=$(echo "$DATA" | grep -oP '"status": "\K[^"]+')

  if [ "$STATUS" == "ACTIVE" ]; then
    log "SSL Certificate is now ACTIVE!"
    echo "$DATA"
    break
  else
    echo -ne "${YELLOW}[#]${NC} Status: ${STATUS} | Time: $(date +%H:%M:%S) (Checking again in 60s)...\r"
  fi
  sleep 60
done