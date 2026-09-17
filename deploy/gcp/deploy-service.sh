#!/usr/bin/env bash
set -euo pipefail

# Redeploy a single Cloud Run service after the first africa-south1 deploy.
# Usage: ./deploy/gcp/deploy-service.sh <service-name>

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
DOMAIN="${DOMAIN:-biscovetech.com}"
REPO_NAME="${REPO_NAME:-realestate-images}"
SHORT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo manual)"
ARTIFACT_REG="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}"
NETWORK="${NETWORK:-default}"
SUBNET="${SUBNET:-default}"
VITE_GOOGLE_MAPS_API_KEY="${VITE_GOOGLE_MAPS_API_KEY:-}"

IDENTITY_CONN_SECRET="identity-service-db-conn-${REGION}"
PROPERTY_CONN_SECRET="property-service-db-conn-${REGION}"
CRM_CONN_SECRET="crm-service-db-conn-${REGION}"
DEVELOPER_CONN_SECRET="developer-api-db-conn-${REGION}"
PROPERTY_PROPERTY_CONN_SECRET="developer-api-property-db-conn-${REGION}"

require_command gcloud
require_command docker

if [ -z "${PROJECT_ID}" ]; then
  error "Set PROJECT_ID before running deploy-service.sh"
fi

if [ "$#" -ne 1 ]; then
  error "Usage: $0 <identity-service|property-service|crm-service|developer-api|gateway|frontend-client|client-facing>"
fi

SERVICE_NAME=$1
gcloud config set project "${PROJECT_ID}" --quiet

dockerfile_for() {
  case "$1" in
    identity-service) echo "backend-services/Identity.Microservice/Dockerfile" ;;
    property-service) echo "backend-services/Property.Microservice/Dockerfile" ;;
    crm-service) echo "backend-services/CRM.Microservice/Dockerfile" ;;
    developer-api) echo "Developer.API/Dockerfile" ;;
    gateway) echo "api-gateway/Dockerfile" ;;
    frontend-client) echo "frontend-client/Dockerfile" ;;
    client-facing) echo "client-facing-app/Dockerfile" ;;
    *) error "Unknown service: $1" ;;
  esac
}

deploy_backend() {
  local name=$1
  local container_port=$2
  local cpu=$3
  local memory=$4
  local min_instances=$5
  local cpu_throttling_flag=$6
  shift 6

  gcloud run deploy "${name}" \
    --image="$(artifact_image "${name}")" \
    --region="${REGION}" \
    --project="${PROJECT_ID}" \
    --platform=managed \
    --service-account="${name}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --port="${container_port}" \
    --network="${NETWORK}" \
    --subnet="${SUBNET}" \
    --vpc-egress=private-ranges-only \
    --execution-environment=gen2 \
    --cpu="${cpu}" \
    --memory="${memory}" \
    --min-instances="${min_instances}" \
    --max-instances=10 \
    "${cpu_throttling_flag}" \
    --allow-unauthenticated \
    --ingress=all \
    "$@" \
    --quiet
}

IDENTITY_URL="$(run_url identity-service)"
PROPERTY_URL="$(run_url property-service)"
CRM_URL="$(run_url crm-service)"
DEVELOPER_URL="$(run_url developer-api)"
GATEWAY_URL="$(run_url gateway)"
FRONTEND_URL="$(run_url frontend-client)"
MARKETING_URL="$(run_url client-facing)"

if [ -n "${DOMAIN}" ]; then
  GATEWAY_PUBLIC_URL="https://api.${DOMAIN}"
  APP_PUBLIC_URL="https://app.${DOMAIN}"
  SITE_PUBLIC_URL="https://www.${DOMAIN}"
else
  GATEWAY_PUBLIC_URL="${GATEWAY_URL}"
  APP_PUBLIC_URL="${FRONTEND_URL}"
  SITE_PUBLIC_URL="${MARKETING_URL}"
fi

case "${SERVICE_NAME}" in
  frontend-client)
    build_and_push_image frontend-client "$(dockerfile_for frontend-client)" \
      --build-arg "VITE_API_BASE_URL=${GATEWAY_PUBLIC_URL}" \
      --build-arg "VITE_GOOGLE_MAPS_API_KEY=${VITE_GOOGLE_MAPS_API_KEY}"
    ;;
  client-facing)
    build_and_push_image client-facing "$(dockerfile_for client-facing)" \
      --build-arg "NEXT_PUBLIC_GATEWAY_URL=${GATEWAY_PUBLIC_URL}" \
      --build-arg "NEXT_PUBLIC_APP_URL=${APP_PUBLIC_URL}" \
      --build-arg "NEXT_PUBLIC_SITE_URL=${SITE_PUBLIC_URL}"
    ;;
  *)
    build_and_push_image "${SERVICE_NAME}" "$(dockerfile_for "${SERVICE_NAME}")"
    ;;
esac

case "${SERVICE_NAME}" in
  identity-service)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" ASPNETCORE_ENVIRONMENT Production
    deploy_backend identity-service 5001 1 1Gi 1 --no-cpu-throttling \
      --env-vars-file="${ENV_FILE}" \
      --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${IDENTITY_CONN_SECRET}:latest"
    rm -f "${ENV_FILE}"
    ;;
  property-service)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" ASPNETCORE_ENVIRONMENT Production
    append_env_var "${ENV_FILE}" Identity__BaseUrl "${IDENTITY_URL}"
    deploy_backend property-service 5002 1 1.5Gi 1 --no-cpu-throttling \
      --env-vars-file="${ENV_FILE}" \
      --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${PROPERTY_CONN_SECRET}:latest"
    rm -f "${ENV_FILE}"
    ;;
  crm-service)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" ASPNETCORE_ENVIRONMENT Production
    append_env_var "${ENV_FILE}" Identity__BaseUrl "${IDENTITY_URL}"
    append_env_var "${ENV_FILE}" Gateway__BaseUrl "${GATEWAY_PUBLIC_URL}"
    deploy_backend crm-service 5003 1 1.5Gi 1 --no-cpu-throttling \
      --env-vars-file="${ENV_FILE}" \
      --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${CRM_CONN_SECRET}:latest"
    rm -f "${ENV_FILE}"
    ;;
  developer-api)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" ASPNETCORE_ENVIRONMENT Production
    deploy_backend developer-api 5006 1 512Mi 0 --cpu-throttling \
      --env-vars-file="${ENV_FILE}" \
      --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DeveloperConnection=${DEVELOPER_CONN_SECRET}:latest,ConnectionStrings__PropertyConnection=${PROPERTY_PROPERTY_CONN_SECRET}:latest"
    rm -f "${ENV_FILE}"
    ;;
  gateway)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" ASPNETCORE_ENVIRONMENT Production
    append_env_var "${ENV_FILE}" DOWNSTREAM_SCHEME https
    append_env_var "${ENV_FILE}" IDENTITY_HOST "$(host_from_url "${IDENTITY_URL}")"
    append_env_var "${ENV_FILE}" IDENTITY_PORT 443
    append_env_var "${ENV_FILE}" PROPERTY_HOST "$(host_from_url "${PROPERTY_URL}")"
    append_env_var "${ENV_FILE}" PROPERTY_PORT 443
    append_env_var "${ENV_FILE}" CRM_HOST "$(host_from_url "${CRM_URL}")"
    append_env_var "${ENV_FILE}" CRM_PORT 443
    append_env_var "${ENV_FILE}" DEVELOPER_HOST "$(host_from_url "${DEVELOPER_URL}")"
    append_env_var "${ENV_FILE}" DEVELOPER_PORT 443
    append_env_var "${ENV_FILE}" Gateway__BaseUrl "${GATEWAY_PUBLIC_URL}"
    CORS_ORIGINS="${FRONTEND_URL},${MARKETING_URL},${GATEWAY_URL}"
    if [ -n "${DOMAIN}" ]; then
      CORS_ORIGINS="https://app.${DOMAIN},https://www.${DOMAIN},${CORS_ORIGINS}"
    fi
    append_env_var "${ENV_FILE}" CORS_ORIGINS "${CORS_ORIGINS}"
    gcloud run deploy gateway \
      --image="$(artifact_image gateway)" \
      --region="${REGION}" \
      --project="${PROJECT_ID}" \
      --service-account="gateway-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
      --port=5000 \
      --cpu=1 \
      --memory=512Mi \
      --min-instances=1 \
      --max-instances=20 \
      --allow-unauthenticated \
      --env-vars-file="${ENV_FILE}" \
      --quiet
    rm -f "${ENV_FILE}"
    ;;
  frontend-client)
    gcloud run deploy frontend-client \
      --image="$(artifact_image frontend-client)" \
      --region="${REGION}" \
      --project="${PROJECT_ID}" \
      --service-account="frontend-client-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
      --port=80 \
      --cpu=1 \
      --memory=512Mi \
      --allow-unauthenticated \
      --quiet
    ;;
  client-facing)
    ENV_FILE="$(mktemp)"
    append_env_var "${ENV_FILE}" NODE_ENV production
    append_env_var "${ENV_FILE}" LEAD_API_URL "${IDENTITY_URL}"
    append_env_var "${ENV_FILE}" NEXT_PUBLIC_GATEWAY_URL "${GATEWAY_PUBLIC_URL}"
    append_env_var "${ENV_FILE}" NEXT_PUBLIC_APP_URL "${APP_PUBLIC_URL}"
    append_env_var "${ENV_FILE}" NEXT_PUBLIC_SITE_URL "${SITE_PUBLIC_URL}"
    gcloud run deploy client-facing \
      --image="$(artifact_image client-facing)" \
      --region="${REGION}" \
      --project="${PROJECT_ID}" \
      --service-account="client-facing-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
      --port=80 \
      --cpu=1 \
      --memory=512Mi \
      --allow-unauthenticated \
      --env-vars-file="${ENV_FILE}" \
      --quiet
    rm -f "${ENV_FILE}"
    ;;
esac

log "Deployed ${SERVICE_NAME} to ${REGION}"
