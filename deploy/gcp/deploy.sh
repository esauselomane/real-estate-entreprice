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
append_env_var "${DEVELOPER_ENV
