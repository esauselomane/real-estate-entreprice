#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  Real Estate Enterprise — Google Cloud deployer
#  Default region: africa-south1 (Johannesburg)
#
#  Provisions:
#   - Artifact Registry
#   - Cloud SQL for SQL Server (private IP, new regional instance)
#   - Secret Manager secrets (reuses jwt-secret / mssql-password)
#   - Service accounts + IAM
#   - Cloud Run services (in-process workers live inside Property/CRM)
#
#  Usage:
#    export PROJECT_ID="project-ff46407b-1a09-4376-95a"
#    export DOMAIN="biscovetech.com"
#    ./deploy/gcp/deploy.sh
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
ZONE="${ZONE:-africa-south1-a}"
DOMAIN="${DOMAIN:-biscovetech.com}"
REPO_NAME="${REPO_NAME:-realestate-images}"
SQL_INSTANCE="${SQL_INSTANCE:-realestate-sql-${REGION}}"
SQL_TIER="${SQL_TIER:-db-custom-1-3840}"
SQL_VERSION="${SQL_VERSION:-SQLSERVER_2022_EXPRESS}"
NETWORK="${NETWORK:-default}"
SUBNET="${SUBNET:-default}"
SHORT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo manual)"
ARTIFACT_REG="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}"
SKIP_DOMAIN_MAPPING="${SKIP_DOMAIN_MAPPING:-0}"
VITE_GOOGLE_MAPS_API_KEY="${VITE_GOOGLE_MAPS_API_KEY:-}"

IDENTITY_CONN_SECRET="identity-service-db-conn-${REGION}"
PROPERTY_CONN_SECRET="property-service-db-conn-${REGION}"
CRM_CONN_SECRET="crm-service-db-conn-${REGION}"
DEVELOPER_CONN_SECRET="developer-api-db-conn-${REGION}"
PROPERTY_PROPERTY_CONN_SECRET="developer-api-property-db-conn-${REGION}"

require_command gcloud
require_command docker
require_command openssl

if [ -z "${PROJECT_ID}" ] || [ "${PROJECT_ID}" = "your-gcp-project-id" ]; then
  error "Set PROJECT_ID to your GCP project (for example project-ff46407b-1a09-4376-95a)"
fi

if [ -z "${DOMAIN}" ] || [ "${DOMAIN}" = "your-production-domain.com" ]; then
  warn "DOMAIN is not set. Frontends will be built against the Cloud Run gateway URL and custom domain mapping will be skipped."
  SKIP_DOMAIN_MAPPING=1
  DOMAIN=""
else
  log "Public hosts: app.${DOMAIN}, api.${DOMAIN}, www.${DOMAIN}"
fi

gcloud config set project "${PROJECT_ID}" --quiet
log "Targeting project ${PROJECT_ID} in ${REGION} (tag ${SHORT_SHA})"

log "Enabling required Google Cloud APIs..."
gcloud services enable \
  run.googleapis.com \
  sqladmin.googleapis.com \
  secretmanager.googleapis.com \
  artifactregistry.googleapis.com \
  cloudbuild.googleapis.com \
  compute.googleapis.com \
  certificatemanager.googleapis.com \
  servicenetworking.googleapis.com \
  dns.googleapis.com \
  --project="${PROJECT_ID}" \
  --quiet

log "Ensuring Artifact Registry repository ${REPO_NAME}..."
gcloud artifacts repositories describe "${REPO_NAME}" \
  --location="${REGION}" \
  --project="${PROJECT_ID}" >/dev/null 2>&1 || \
  gcloud artifacts repositories create "${REPO_NAME}" \
    --repository-format=docker \
    --location="${REGION}" \
    --description="Real Estate Enterprise container images (${REGION})" \
    --project="${PROJECT_ID}" \
    --quiet
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

log "Resolving JWT and SQL credentials from Secret Manager..."
if secret_exists jwt-secret; then
  JWT_SECRET="$(read_secret jwt-secret)"
else
  JWT_SECRET="${JWT_SECRET:-$(openssl rand -base64 48 | tr -d '/+=' )}"
  ensure_secret_if_missing jwt-secret "${JWT_SECRET}"
fi

if secret_exists mssql-password; then
  SQL_PASSWORD="$(read_secret mssql-password)"
else
  SQL_PASSWORD="${SQL_PASSWORD:-$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-24)}"
  ensure_secret_if_missing mssql-password "${SQL_PASSWORD}"
fi

if [ -n "${DOMAIN}" ]; then
  ensure_secret_if_missing support-email "support@${DOMAIN}"
fi

log "Configuring Private Services Access for Cloud SQL..."
gcloud compute addresses describe google-managed-services-default \
  --global \
  --project="${PROJECT_ID}" >/dev/null 2>&1 || \
  gcloud compute addresses create google-managed-services-default \
    --global \
    --purpose=VPC_PEERING \
    --prefix-length=16 \
    --description="Private services range for Cloud SQL" \
    --network="${NETWORK}" \
    --project="${PROJECT_ID}" \
    --quiet

gcloud services vpc-peerings connect \
  --service=servicenetworking.googleapis.com \
  --ranges=google-managed-services-default \
  --network="${NETWORK}" \
  --project="${PROJECT_ID}" \
  --quiet 2>/dev/null || true

create_sql_instance_if_needed() {
  if gcloud sql instances describe "${SQL_INSTANCE}" --project="${PROJECT_ID}" >/dev/null 2>&1; then
    local existing_region
    existing_region="$(gcloud sql instances describe "${SQL_INSTANCE}" --project="${PROJECT_ID}" --format='value(region)')"
    if [ "${existing_region}" != "${REGION}" ]; then
      error "Cloud SQL instance ${SQL_INSTANCE} already exists in ${existing_region}. Set SQL_INSTANCE to a new name for ${REGION}."
    fi
    log "Using existing Cloud SQL instance ${SQL_INSTANCE}"
    return 0
  fi

  log "Creating Cloud SQL SQL Server instance ${SQL_INSTANCE} in ${REGION} (several minutes)..."
  gcloud sql instances create "${SQL_INSTANCE}" \
    --database-version="${SQL_VERSION}" \
    --tier="${SQL_TIER}" \
    --region="${REGION}" \
    --zone="${ZONE}" \
    --root-password="${SQL_PASSWORD}" \
    --availability-type=ZONAL \
    --network="projects/${PROJECT_ID}/global/networks/${NETWORK}" \
    --no-assign-ip \
    --project="${PROJECT_ID}" \
    --quiet
}

create_sql_instance_if_needed

log "Waiting for Cloud SQL instance to become RUNNABLE..."
SQL_STATE=""
while [ "${SQL_STATE}" != "RUNNABLE" ]; do
  SQL_STATE="$(gcloud sql instances describe "${SQL_INSTANCE}" --project="${PROJECT_ID}" --format='value(state)')"
  if [ "${SQL_STATE}" != "RUNNABLE" ]; then
    sleep 15
  fi
done

SQL_PRIVATE_IP="$(sql_private_ip)"
if [ -z "${SQL_PRIVATE_IP}" ]; then
  error "Cloud SQL instance ${SQL_INSTANCE} has no private IP. Check VPC peering and recreate with --no-assign-ip."
fi
log "Cloud SQL private IP is available"

for db in IdentityDB PropertyDB CRMDB DeveloperDB; do
  gcloud sql databases create "${db}" \
    --instance="${SQL_INSTANCE}" \
    --project="${PROJECT_ID}" \
    --quiet 2>/dev/null || warn "${db} already exists"
done

write_secret "${IDENTITY_CONN_SECRET}" "$(sql_connection_string IdentityDB)"
write_secret "${PROPERTY_CONN_SECRET}" "$(sql_connection_string PropertyDB)"
write_secret "${CRM_CONN_SECRET}" "$(sql_connection_string CRMDB)"
write_secret "${DEVELOPER_CONN_SECRET}" "$(sql_connection_string DeveloperDB)"
write_secret "${PROPERTY_PROPERTY_CONN_SECRET}" "$(sql_connection_string PropertyDB)"

log "Creating service accounts..."
ensure_service_account identity-service-sa "Identity Cloud Run"
ensure_service_account property-service-sa "Property Cloud Run"
ensure_service_account crm-service-sa "CRM Cloud Run"
ensure_service_account developer-api-sa "Developer API Cloud Run"
ensure_service_account gateway-sa "Gateway Cloud Run"
ensure_service_account frontend-client-sa "Frontend Cloud Run"
ensure_service_account client-facing-sa "Marketing Cloud Run"

log "Binding Cloud SQL and secret access..."
for account_id in identity-service-sa property-service-sa crm-service-sa developer-api-sa; do
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${account_id}@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="roles/cloudsql.client" \
    --condition=None \
    --quiet >/dev/null
  grant_secret_accessor "${account_id}@${PROJECT_ID}.iam.gserviceaccount.com" jwt-secret
done

grant_secret_accessor "identity-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${IDENTITY_CONN_SECRET}"
grant_secret_accessor "property-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${PROPERTY_CONN_SECRET}"
grant_secret_accessor "crm-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${CRM_CONN_SECRET}"
grant_secret_accessor "developer-api-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${DEVELOPER_CONN_SECRET}"
grant_secret_accessor "developer-api-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${PROPERTY_PROPERTY_CONN_SECRET}"

deploy_backend() {
  local name=$1
  local container_port=$2
  local service_account=$3
  local cpu=$4
  local memory=$5
  local min_instances=$6
  local cpu_throttling_flag=$7
  shift 7

  log "Deploying ${name}..."
  gcloud run deploy "${name}" \
    --image="$(artifact_image "${name}")" \
    --region="${REGION}" \
    --project="${PROJECT_ID}" \
    --platform=managed \
    --service-account="${service_account}@${PROJECT_ID}.iam.gserviceaccount.com" \
    --port="${container_port}" \
    --network="${NETWORK}" \
    --subnet="${SUBNET}" \
    --vpc-egress=private-ranges-only \
    --execution-environment=gen2 \
    --cpu="${cpu}" \
    --memory="${memory}" \
    --min-instances="${min_instances}" \
    --max-instances=10 \
    --concurrency=80 \
    --timeout=300 \
    "${cpu_throttling_flag}" \
    --allow-unauthenticated \
    --ingress=all \
    "$@" \
    --quiet
}

log "Building backend images..."
build_and_push_image identity-service backend-services/Identity.Microservice/Dockerfile
build_and_push_image property-service backend-services/Property.Microservice/Dockerfile
build_and_push_image crm-service backend-services/CRM.Microservice/Dockerfile
build_and_push_image developer-api Developer.API/Dockerfile
build_and_push_image gateway api-gateway/Dockerfile

IDENTITY_ENV="$(mktemp)"
append_env_var "${IDENTITY_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${IDENTITY_ENV}" Jwt__Issuer Identity.Microservice
append_env_var "${IDENTITY_ENV}" Jwt__Audience RealEstateApp.Client
if [ -n "${DOMAIN}" ]; then
  append_env_var "${IDENTITY_ENV}" Cors__AllowedOrigins__0 "https://app.${DOMAIN}"
  append_env_var "${IDENTITY_ENV}" Cors__AllowedOrigins__1 "https://www.${DOMAIN}"
fi

deploy_backend identity-service 5001 identity-service-sa 1 1Gi 1 --no-cpu-throttling \
  --env-vars-file="${IDENTITY_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${IDENTITY_CONN_SECRET}:latest"

IDENTITY_URL="$(run_url identity-service)"
IDENTITY_HOST="$(host_from_url "${IDENTITY_URL}")"
[ -n "${IDENTITY_URL}" ] || error "identity-service did not return a URL"

PROPERTY_ENV="$(mktemp)"
append_env_var "${PROPERTY_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${PROPERTY_ENV}" Identity__BaseUrl "${IDENTITY_URL}"

deploy_backend property-service 5002 property-service-sa 1 1.5Gi 1 --no-cpu-throttling \
  --env-vars-file="${PROPERTY_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${PROPERTY_CONN_SECRET}:latest"

CRM_ENV="$(mktemp)"
append_env_var "${CRM_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${CRM_ENV}" Identity__BaseUrl "${IDENTITY_URL}"

deploy_backend crm-service 5003 crm-service-sa 1 1.5Gi 1 --no-cpu-throttling \
  --env-vars-file="${CRM_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${CRM_CONN_SECRET}:latest"

DEVELOPER_ENV="$(mktemp)"
append_env_var "${DEVELOPER_ENV}" ASPNETCORE_ENVIRONMENT Production

deploy_backend developer-api 5006 developer-api-sa 1 512Mi 0 --cpu-throttling \
  --env-vars-file="${DEVELOPER_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DeveloperConnection=${DEVELOPER_CONN_SECRET}:latest,ConnectionStrings__PropertyConnection=${PROPERTY_PROPERTY_CONN_SECRET}:latest"

PROPERTY_URL="$(run_url property-service)"
CRM_URL="$(run_url crm-service)"
DEVELOPER_URL="$(run_url developer-api)"
PROPERTY_HOST="$(host_from_url "${PROPERTY_URL}")"
CRM_HOST="$(host_from_url "${CRM_URL}")"
DEVELOPER_HOST="$(host_from_url "${DEVELOPER_URL}")"

GATEWAY_PUBLIC_URL="https://api.${DOMAIN}"
if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  GATEWAY_PUBLIC_URL=""
fi

deploy_gateway() {
  local cors_origins=$1
  local gateway_base=$2
  local gateway_env
  gateway_env="$(mktemp)"
  append_env_var "${gateway_env}" ASPNETCORE_ENVIRONMENT Production
  append_env_var "${gateway_env}" DOWNSTREAM_SCHEME https
  append_env_var "${gateway_env}" IDENTITY_HOST "${IDENTITY_HOST}"
  append_env_var "${gateway_env}" IDENTITY_PORT 443
  append_env_var "${gateway_env}" PROPERTY_HOST "${PROPERTY_HOST}"
  append_env_var "${gateway_env}" PROPERTY_PORT 443
  append_env_var "${gateway_env}" CRM_HOST "${CRM_HOST}"
  append_env_var "${gateway_env}" CRM_PORT 443
  append_env_var "${gateway_env}" DEVELOPER_HOST "${DEVELOPER_HOST}"
  append_env_var "${gateway_env}" DEVELOPER_PORT 443
  append_env_var "${gateway_env}" Gateway__BaseUrl "${gateway_base}"
  append_env_var "${gateway_env}" CORS_ORIGINS "${cors_origins}"

  log "Deploying gateway..."
  gcloud run deploy gateway \
    --image="$(artifact_image gateway)" \
    --region="${REGION}" \
    --project="${PROJECT_ID}" \
    --platform=managed \
    --service-account="gateway-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --port=5000 \
    --execution-environment=gen2 \
    --cpu=1 \
    --memory=512Mi \
    --min-instances=1 \
    --max-instances=20 \
    --concurrency=200 \
    --timeout=60 \
    --allow-unauthenticated \
    --ingress=all \
    --env-vars-file="${gateway_env}" \
    --quiet
  rm -f "${gateway_env}"
}

INITIAL_CORS="http://localhost:5173,http://localhost:3000"
if [ -n "${DOMAIN}" ]; then
  INITIAL_CORS="https://app.${DOMAIN},https://www.${DOMAIN},${INITIAL_CORS}"
fi

deploy_gateway "${INITIAL_CORS}" "${GATEWAY_PUBLIC_URL:-https://placeholder.invalid}"
GATEWAY_URL="$(run_url gateway)"
[ -n "${GATEWAY_URL}" ] || error "gateway did not return a URL"
if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  GATEWAY_PUBLIC_URL="${GATEWAY_URL}"
fi

FRONTEND_API_URL="${GATEWAY_PUBLIC_URL}"
APP_PUBLIC_URL="https://app.${DOMAIN}"
SITE_PUBLIC_URL="https://www.${DOMAIN}"
if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  APP_PUBLIC_URL="${GATEWAY_URL}"
  SITE_PUBLIC_URL="${GATEWAY_URL}"
fi

log "Building frontend images with public API URL ${FRONTEND_API_URL}..."
FORCE_REBUILD=1
FRONTEND_BUILD_ARGS=(--build-arg "VITE_API_BASE_URL=${FRONTEND_API_URL}")
if [ -n "${VITE_GOOGLE_MAPS_API_KEY}" ]; then
  FRONTEND_BUILD_ARGS+=(--build-arg "VITE_GOOGLE_MAPS_API_KEY=${VITE_GOOGLE_MAPS_API_KEY}")
fi
build_and_push_image frontend-client frontend-client/Dockerfile "${FRONTEND_BUILD_ARGS[@]}"
build_and_push_image client-facing client-facing-app/Dockerfile \
  --build-arg "NEXT_PUBLIC_GATEWAY_URL=${FRONTEND_API_URL}" \
  --build-arg "NEXT_PUBLIC_APP_URL=${APP_PUBLIC_URL}" \
  --build-arg "NEXT_PUBLIC_SITE_URL=${SITE_PUBLIC_URL}"

log "Deploying frontend-client..."
gcloud run deploy frontend-client \
  --image="$(artifact_image frontend-client)" \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --service-account="frontend-client-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --port=80 \
  --cpu=1 \
  --memory=512Mi \
  --min-instances=0 \
  --max-instances=10 \
  --allow-unauthenticated \
  --ingress=all \
  --quiet

log "Deploying client-facing..."
CLIENT_ENV="$(mktemp)"
append_env_var "${CLIENT_ENV}" NODE_ENV production
append_env_var "${CLIENT_ENV}" LEAD_API_URL "${IDENTITY_URL}"
append_env_var "${CLIENT_ENV}" NEXT_PUBLIC_GATEWAY_URL "${FRONTEND_API_URL}"
append_env_var "${CLIENT_ENV}" NEXT_PUBLIC_APP_URL "${APP_PUBLIC_URL}"
append_env_var "${CLIENT_ENV}" NEXT_PUBLIC_SITE_URL "${SITE_PUBLIC_URL}"
gcloud run deploy client-facing \
  --image="$(artifact_image client-facing)" \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --service-account="client-facing-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --port=80 \
  --cpu=1 \
  --memory=512Mi \
  --min-instances=0 \
  --max-instances=10 \
  --allow-unauthenticated \
  --ingress=all \
  --env-vars-file="${CLIENT_ENV}" \
  --quiet

FRONTEND_URL="$(run_url frontend-client)"
MARKETING_URL="$(run_url client-facing)"

FINAL_CORS="${FRONTEND_URL},${MARKETING_URL},${GATEWAY_URL}"
if [ -n "${DOMAIN}" ]; then
  FINAL_CORS="https://app.${DOMAIN},https://www.${DOMAIN},${FINAL_CORS}"
fi
deploy_gateway "${FINAL_CORS}" "${GATEWAY_PUBLIC_URL}"

gcloud run services update crm-service \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --update-env-vars="Gateway__BaseUrl=${GATEWAY_PUBLIC_URL}" \
  --quiet

if [ "${SKIP_DOMAIN_MAPPING}" != "1" ]; then
  log "Mapping custom domains..."
  gcloud beta run domain-mappings create --service=frontend-client --domain="app.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "frontend-client domain mapping already exists or is unavailable in ${REGION}"
  gcloud beta run domain-mappings create --service=client-facing --domain="www.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "client-facing domain mapping already exists or is unavailable in ${REGION}"
  gcloud beta run domain-mappings create --service=gateway --domain="api.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "gateway domain mapping already exists or is unavailable in ${REGION}"
fi

rm -f "${IDENTITY_ENV}" "${PROPERTY_ENV}" "${CRM_ENV}" "${DEVELOPER_ENV}" "${CLIENT_ENV}"

cat <<EOF

${GREEN}========================================${NC}
${GREEN}  DEPLOYMENT COMPLETE${NC}
${GREEN}========================================${NC}

Project:     ${PROJECT_ID}
Region:      ${REGION}
SQL instance: ${SQL_INSTANCE}
Image tag:   ${SHORT_SHA}

Service URLs:
  Identity:   ${IDENTITY_URL}
  Property:   ${PROPERTY_URL}
  CRM:        ${CRM_URL}
  Developer:  ${DEVELOPER_URL}
  Gateway:    ${GATEWAY_URL}
  Frontend:   ${FRONTEND_URL}
  Marketing:  ${MARKETING_URL}

EOF

if [ "${SKIP_DOMAIN_MAPPING}" != "1" ]; then
  cat <<EOF
Custom domains (add DNS records at your registrar after mapping output):
  app.${DOMAIN}  → frontend-client
  www.${DOMAIN}  → client-facing
  api.${DOMAIN}  → gateway

EOF
fi

cat <<EOF
${YELLOW}Notes:${NC}
  - Background jobs run inside Property and CRM (min instances = 1). Separate Kafka workers are not deployed.
  - SQL Server uses private IP over Direct VPC egress, not the Cloud SQL Unix socket.
  - Connection strings are stored in regional Secret Manager secrets; jwt-secret was reused if it already existed.
  - Uploaded property files live on the Cloud Run container disk and are not durable across revisions. Move the vault to Cloud Storage before production document use.
  - Leave the us-central1 SQL instance in place until these Johannesburg health checks succeed, then delete it separately.

${GREEN}Verify with:${NC}
  curl -i ${GATEWAY_URL}/health
  curl -i ${IDENTITY_URL}/health
  curl -i ${PROPERTY_URL}/health
EOF
