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

RESEND_SECRET_VERSION="${RESEND_SECRET_VERSION:-3}"
# Use the latest PayFast secrets unless a version override is provided.
PAYFAST_MERCHANT_ID_SECRET_VERSION="${PAYFAST_MERCHANT_ID_SECRET_VERSION:-latest}"
PAYFAST_MERCHANT_KEY_SECRET_VERSION="${PAYFAST_MERCHANT_KEY_SECRET_VERSION:-latest}"
PAYFAST_PASSPHRASE_SECRET_VERSION="${PAYFAST_PASSPHRASE_SECRET_VERSION:-latest}"

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
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
VITE_GOOGLE_MAPS_API_KEY="${VITE_GOOGLE_MAPS_API_KEY:-AIzaSyA_ISwPvfaEwCvFl0Q3yCneBa6s_sLuYBk}"
FORCE_REBUILD="${FORCE_REBUILD:-1}"

IDENTITY_CONN_SECRET="identity-service-db-conn-${REGION}"
PROPERTY_CONN_SECRET="property-service-db-conn-${REGION}"
CRM_CONN_SECRET="crm-service-db-conn-${REGION}"

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

# Compute specific origins for production to allow credentials (cookies/auth headers)
# This must be defined BEFORE the services are deployed.
if [ -n "${DOMAIN}" ]; then
  INITIAL_CORS="https://app.${DOMAIN},https://www.${DOMAIN},https://${DOMAIN},https://api.${DOMAIN}"
  GATEWAY_PUBLIC_URL="https://api.${DOMAIN}/gateway"
else
  INITIAL_CORS="*"
  GATEWAY_PUBLIC_URL="" # Will be resolved via run_url
fi

if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  APP_PUBLIC_URL="" # Will be resolved via run_url
  SITE_PUBLIC_URL="" # Will be resolved via run_url
fi

gcloud config set project "${PROJECT_ID}" --quiet
log "Targeting project ${PROJECT_ID} in ${REGION} (tag ${SHORT_SHA})"
log "Force rebuild is set to: ${FORCE_REBUILD}"

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
  cloudresourcemanager.googleapis.com \
  --project="${PROJECT_ID}" \
  --quiet

log "Ensuring Artifact Registry repository ${REPO_NAME}..."
gcloud artifacts repositories describe "${REPO_NAME}" \
  --location="${REGION}" \
  --project="${PROJECT_ID}" >/dev/null 2>&1 || \
  gcloud artifacts repositories create "${REPO_NAME}" \
    --repository-format=docker \
    --location="${REGION}" \
    --description="Biscove Technologies container images (${REGION})" \
    --project="${PROJECT_ID}" \
    --quiet

log "Configuring Docker credential helper for Artifact Registry..."
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

log "Configuring and verifying permissions for default build service account..."
PROJECT_NUMBER="$(gcloud projects describe "${PROJECT_ID}" --format='value(projectNumber)')"
COMPUTE_ENGINE_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"

# 1. Bucket Source Read Access
gcloud storage buckets add-iam-policy-binding "gs://${PROJECT_ID}_cloudbuild" \
  --member="serviceAccount:${COMPUTE_ENGINE_SA}" \
  --role="roles/storage.objectViewer" --project="${PROJECT_ID}" --quiet || warn "Storage read binding skipped/exists."

# 2. FIX: Added logging permissions with the required --condition=None flag
gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
  --member="serviceAccount:${COMPUTE_ENGINE_SA}" \
  --role="roles/logging.logWriter" \
  --condition=None \
  --project="${PROJECT_ID}" --quiet || warn "Logging binding skipped/exists."

# 3. FIX: Added Artifact Registry permissions with the required --condition=None flag
gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
  --member="serviceAccount:${COMPUTE_ENGINE_SA}" \
  --role="roles/artifactregistry.writer" \
  --condition=None \
  --project="${PROJECT_ID}" --quiet || warn "Artifact Registry binding skipped/exists."

log "Ensuring Private Google Access is enabled for subnet..."
TARGET_SUBNET="${SUBNET}"
gcloud compute networks subnets update "${TARGET_SUBNET}" \
  --region="${REGION}" \
  --enable-private-ip-google-access \
  --project="${PROJECT_ID}" --quiet || warn "Subnet ${TARGET_SUBNET} already enabled or not found."

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

if ! secret_exists resend-api-token; then
  error "Create the resend-api-token secret in Secret Manager before deploying. See deploy/gcp/README.md."
fi

for payfast_secret in payfast-merchant-id payfast-merchant-key payfast-passphrase; do
  if ! secret_exists "${payfast_secret}"; then
    error "Create the ${payfast_secret} secret in Secret Manager before deploying. See deploy/gcp/README.md."
  fi
done

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

log "Checking VPC peering..."
if ! gcloud services vpc-peerings list --network="${NETWORK}" --project="${PROJECT_ID}" | grep -q "servicenetworking.googleapis.com"; then
  gcloud services vpc-peerings connect \
    --service=servicenetworking.googleapis.com \
    --ranges=google-managed-services-default \
    --network="${NETWORK}" \
    --project="${PROJECT_ID}" \
    --quiet
fi
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

log "Creating service accounts..."
ensure_service_account identity-service-sa "Identity Cloud Run"
ensure_service_account property-service-sa "Property Cloud Run"
ensure_service_account crm-service-sa "CRM Cloud Run"
ensure_service_account gateway-sa "Gateway Cloud Run"
ensure_service_account frontend-client-sa "Frontend Cloud Run"
ensure_service_account client-facing-sa "Marketing Cloud Run"

log "Binding Cloud SQL and secret access..."
for account_id in identity-service-sa property-service-sa crm-service-sa; do
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
grant_secret_accessor "identity-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" resend-api-token
grant_secret_accessor "crm-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" resend-api-token
for payfast_secret in payfast-merchant-id payfast-merchant-key payfast-passphrase; do
  grant_secret_accessor "identity-service-sa@${PROJECT_ID}.iam.gserviceaccount.com" "${payfast_secret}"
done

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

# ============================================================
#  Part 2 of 2: Building and Deploying All Services
# ============================================================

log "Building all service images..."
build_and_push_image identity-service "backend-services/Identity.Microservice/Dockerfile"
build_and_push_image property-service "backend-services/Property.Microservice/Dockerfile"
build_and_push_image crm-service "backend-services/CRM.Microservice/Dockerfile"
build_and_push_image gateway api-gateway/Dockerfile
log "Deploying identity-service..."
IDENTITY_ENV="$(mktemp)"
append_env_var "${IDENTITY_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${IDENTITY_ENV}" CORS_ORIGINS "${INITIAL_CORS}"
append_env_var "${IDENTITY_ENV}" Cors__AllowedOrigins "${INITIAL_CORS}"
append_env_var "${IDENTITY_ENV}" AllowedOrigins "${INITIAL_CORS}"
append_env_var "${IDENTITY_ENV}" Gateway__BaseUrl "${GATEWAY_PUBLIC_URL}"
deploy_backend identity-service 5001 identity-service-sa 1 1Gi 1 --no-cpu-throttling \
  --env-vars-file="${IDENTITY_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${IDENTITY_CONN_SECRET}:latest,Resend__ApiToken=resend-api-token:${RESEND_SECRET_VERSION},PayFast__MerchantId=payfast-merchant-id:${PAYFAST_MERCHANT_ID_SECRET_VERSION},PayFast__MerchantKey=payfast-merchant-key:${PAYFAST_MERCHANT_KEY_SECRET_VERSION},PayFast__PassPhrase=payfast-passphrase:${PAYFAST_PASSPHRASE_SECRET_VERSION}"
rm -f "${IDENTITY_ENV}"
IDENTITY_URL="$(run_url identity-service)"
IDENTITY_HOST="$(host_from_url "${IDENTITY_URL}")"

log "Deploying property-service..."
PROPERTY_ENV="$(mktemp)"
append_env_var "${PROPERTY_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${PROPERTY_ENV}" Identity__BaseUrl "${IDENTITY_URL}"
append_env_var "${PROPERTY_ENV}" CORS_ORIGINS "${INITIAL_CORS}"
append_env_var "${PROPERTY_ENV}" Cors__AllowedOrigins "${INITIAL_CORS}"
deploy_backend property-service 5002 property-service-sa 1 1.5Gi 1 --no-cpu-throttling \
  --env-vars-file="${PROPERTY_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${PROPERTY_CONN_SECRET}:latest"
rm -f "${PROPERTY_ENV}"
PROPERTY_URL="$(run_url property-service)"
PROPERTY_HOST="$(host_from_url "${PROPERTY_URL}")"

log "Deploying crm-service..."
CRM_ENV="$(mktemp)"
append_env_var "${CRM_ENV}" ASPNETCORE_ENVIRONMENT Production
append_env_var "${CRM_ENV}" Identity__BaseUrl "${IDENTITY_URL}"
append_env_var "${CRM_ENV}" CORS_ORIGINS "${INITIAL_CORS}"
append_env_var "${CRM_ENV}" Cors__AllowedOrigins "${INITIAL_CORS}"
deploy_backend crm-service 5003 crm-service-sa 1 1.5Gi 1 --no-cpu-throttling \
  --env-vars-file="${CRM_ENV}" \
  --set-secrets="Jwt__Secret=jwt-secret:latest,ConnectionStrings__DefaultConnection=${CRM_CONN_SECRET}:latest,Resend__ApiToken=resend-api-token:${RESEND_SECRET_VERSION}"
rm -f "${CRM_ENV}"
CRM_URL="$(run_url crm-service)"
CRM_HOST="$(host_from_url "${CRM_URL}")"

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
  append_env_var "${gateway_env}" Gateway__BaseUrl "${gateway_base}"
  append_env_var "${gateway_env}" CORS_ORIGINS "${cors_origins}"
  append_env_var "${gateway_env}" Cors__AllowedOrigins "${cors_origins}"
  append_env_var "${gateway_env}" AllowedOrigins "${cors_origins}"
  append_env_var "${gateway_env}" ASPNETCORE_URLS "http://*:5000"

  log "Deploying gateway..."
  # FIX: Ensure old VPC connector and any specific ingress settings are reset
  gcloud run services update gateway --clear-vpc-connector --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null || true

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
deploy_gateway "${INITIAL_CORS}" "${GATEWAY_PUBLIC_URL:-https://placeholder.invalid}"

GATEWAY_URL="$(run_url gateway)"
[ -n "${GATEWAY_URL}" ] || error "gateway did not return a URL"

if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  GATEWAY_PUBLIC_URL="${GATEWAY_URL}/gateway"
fi

# FIX: Redeploy the gateway now that we have the actual public URL. 
# Ocelot and the Identity service need this for correct route matching and token validation.
if [ -z "${DOMAIN}" ] || [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
    log "Redeploying gateway with actual URL: ${GATEWAY_PUBLIC_URL}"
    deploy_gateway "${INITIAL_CORS}" "${GATEWAY_PUBLIC_URL}"
fi

FRONTEND_API_URL="${GATEWAY_PUBLIC_URL}"
APP_PUBLIC_URL="https://app.${DOMAIN}"
SITE_PUBLIC_URL="https://www.${DOMAIN}"
if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  APP_PUBLIC_URL="${GATEWAY_URL}"
  SITE_PUBLIC_URL="${GATEWAY_URL}"
fi
CLIENT_GATEWAY_PUBLIC_URL="${GATEWAY_PUBLIC_URL%/gateway}"
LEAD_API_URL="${FRONTEND_API_URL%/}/system/lead-requests"
ONBOARDING_API_URL="${FRONTEND_API_URL%/}/auth"

log "Building frontend images with public API URL ${FRONTEND_API_URL}..."
FRONTEND_BUILD_ARGS=(--build-arg "VITE_API_BASE_URL=${FRONTEND_API_URL}")
if [ -n "${VITE_GOOGLE_MAPS_API_KEY}" ]; then
  FRONTEND_BUILD_ARGS+=(--build-arg "VITE_GOOGLE_MAPS_API_KEY=${VITE_GOOGLE_MAPS_API_KEY}")
fi
build_and_push_image frontend-client frontend-client/Dockerfile "${FRONTEND_BUILD_ARGS[@]}"
build_and_push_image client-facing client-facing-app/Dockerfile \
  --build-arg "NEXT_PUBLIC_GATEWAY_URL=${CLIENT_GATEWAY_PUBLIC_URL}" \
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
append_env_var "${CLIENT_ENV}" LEAD_API_URL "${LEAD_API_URL}"
append_env_var "${CLIENT_ENV}" ONBOARDING_API_URL "${ONBOARDING_API_URL}"
append_env_var "${CLIENT_ENV}" NEXT_PUBLIC_GATEWAY_URL "${CLIENT_GATEWAY_PUBLIC_URL}"
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
EMAIL_APP_URL="https://app.${DOMAIN}"
if [ "${SKIP_DOMAIN_MAPPING}" = "1" ]; then
  EMAIL_APP_URL="${FRONTEND_URL}"
fi
gcloud run services update identity-service \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --update-env-vars="Urls__FrontendClient=${EMAIL_APP_URL},SampleData__PropertyServiceUrl=${PROPERTY_URL},SampleData__CrmServiceUrl=${CRM_URL}" \
  --quiet
gcloud run services update crm-service \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --update-env-vars="Gateway__BaseUrl=${GATEWAY_PUBLIC_URL},Frontend__BaseUrl=${EMAIL_APP_URL}" \
  --quiet

if [ "${SKIP_DOMAIN_MAPPING}" != "1" ] && [ "${REGION}" != "africa-south1" ]; then
  log "Mapping custom domains..."
  gcloud beta run domain-mappings create --service=frontend-client --domain="app.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "frontend-client domain mapping already exists or failed."
  gcloud beta run domain-mappings create --service=gateway --domain="api.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "gateway domain mapping already exists or failed."
  gcloud beta run domain-mappings create --service=client-facing --domain="www.${DOMAIN}" --region="${REGION}" --project="${PROJECT_ID}" --quiet 2>/dev/null \
    || warn "client-facing domain mapping already exists or failed."
elif [ "${REGION}" == "africa-south1" ]; then
  log "Skipping legacy domain mappings: feature not supported in ${REGION}. Use deploy-load-balancer.sh instead."
fi

log "Deployment of all services complete."
