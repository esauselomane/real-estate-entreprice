#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  Real Estate Enterprise — Individual Service Deployer
# ============================================================
#  Usage:
#    ./deploy/gcp/deploy-service.sh <service-name>
#
#  Available Services:
#    identity-service, property-service, crm-service, 
#    developer-api, gateway, brochure-worker, image-worker, 
#    syndication-worker, frontend-client, client-facing
# ============================================================

# --- Configuration ---
PROJECT_ID="${PROJECT_ID:-your-gcp-project-id}"
REGION="${REGION:-us-central1}"
DOMAIN="${DOMAIN:-your-production-domain.com}"
KAFKA_BOOTSTRAP_SERVERS="${KAFKA_BOOTSTRAP_SERVERS:-your-kafka-broker:9092}"
REPO_NAME="realestate-images"
SQL_INSTANCE="realestate-sql"
SHORT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo manual)"
ARTIFACT_REG="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}"

# --- Color output ---
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
log()   { echo -e "${GREEN}[+]${NC} $*"; }
warn()  { echo -e "${YELLOW}[!]${NC} $*"; }
error() { echo -e "${RED}[-]${NC} $*" >&2; exit 1; }

# --- Pre-flight ---
if [ "$#" -ne 1 ]; then
    error "Usage: $0 <service-name>\nExample: $0 syndication-worker"
fi

SERVICE_NAME=$1

declare -A SERVICES=(
  ["identity-service"]="backend-services/Identity.Microservice/Dockerfile"
  ["property-service"]="backend-services/Property.Microservice/Dockerfile"
  ["crm-service"]="backend-services/CRM.Microservice/Dockerfile"
  ["developer-api"]="Developer.API/Dockerfile"
  ["gateway"]="api-gateway/Dockerfile"
  ["brochure-worker"]="backend-services/BrochureGeneration.Worker/Dockerfile"
  ["image-worker"]="backend-services/ImageProcessor.Worker/Dockerfile"
  ["syndication-worker"]="backend-services/PlatformSyndication.Worker/Dockerfile"
  ["frontend-client"]="frontend-client/Dockerfile"
  ["client-facing"]="client-facing-app/Dockerfile"
)

if [[ -z "${SERVICES[$SERVICE_NAME]:-}" ]]; then
    error "Unknown service: ${SERVICE_NAME}. Available: ${!SERVICES[@]}"
fi

command -v gcloud >/dev/null || error "gcloud CLI is required"
command -v docker  >/dev/null || error "docker is required"
gcloud config set project "${PROJECT_ID}" --quiet

# Fetch SQL Password from Secret Manager
log "Fetching deployment credentials..."
SQL_PASSWORD=$(gcloud secrets versions access latest --secret=mssql-password 2>/dev/null || error "Could not find mssql-password secret. Run full deploy.sh first.")

# Verify Service Account exists before building/pushing
SA_EMAIL="${SERVICE_NAME}-sa@${PROJECT_ID}.iam.gserviceaccount.com"
log "Verifying service account: ${SA_EMAIL}"
gcloud iam service-accounts describe "${SA_EMAIL}" --quiet >/dev/null 2>&1 || error "Service account ${SA_EMAIL} not found. Run full deploy.sh first."

deploy_service() {
  local name=$1
  local db_name=$2
  shift 2
  log "Deploying ${name} to Cloud Run..."
  gcloud run deploy "${name}" \
    --image="${ARTIFACT_REG}/${name}:${SHORT_SHA}" \
    --region="${REGION}" \
    --platform=managed \
    --service-account="${name}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --vpc-connector=realestate-connector \
    --add-cloudsql-instances="${PROJECT_ID}:${REGION}:${SQL_INSTANCE}" \
    --execution-environment=gen2 \
    --cpu=2 \
    --memory=2Gi \
    --min-instances=0 \
    --max-instances=10 \
    --concurrency=80 \
    --timeout=300 \
    --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,ASPNETCORE_URLS=http://*:8080" \
    --set-env-vars="ConnectionStrings__DefaultConnection=Server=127.0.0.1;Database=${db_name};User Id=sqlserver;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;,Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}" \
    --set-secrets="Jwt__Secret=jwt-secret:latest" \
    "$@" \
    --quiet
}

deploy_worker() {
  local name=$1
  shift
  log "Deploying worker ${name} to Cloud Run..."
  gcloud run deploy "${name}" \
    --image="${ARTIFACT_REG}/${name}:${SHORT_SHA}" \
    --region="${REGION}" \
    --platform=managed \
    --service-account="${name}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --no-allow-unauthenticated \
    --vpc-connector=realestate-connector \
    --min-instances=0 \
    --max-instances=5 \
    --cpu=2 \
    --memory=2Gi \
    --timeout=3600 \
    "$@" \
    --quiet
}

get_url() {
    gcloud run services describe "$1" --region="${REGION}" --format='value(status.url)' 2>/dev/null || echo "pending"
}

# --- Build & Push ---
log "Checking if image version ${SHORT_SHA} exists in registry..."
if gcloud artifacts docker images list "${ARTIFACT_REG}/${SERVICE_NAME}" --filter="TAGS:${SHORT_SHA}" --format="get(package)" 2>/dev/null | grep -q "${SERVICE_NAME}"; then
    log "Image ${SERVICE_NAME}:${SHORT_SHA} already exists. Skipping build and push."
else
    dockerfile="${SERVICES[$SERVICE_NAME]}"
    log "Building ${SERVICE_NAME}..."
    docker build \
      --build-arg BUILDCONFIGURATION=Release \
      -t "${ARTIFACT_REG}/${SERVICE_NAME}:${SHORT_SHA}" \
      -t "${ARTIFACT_REG}/${SERVICE_NAME}:latest" \
      -f "${dockerfile}" \
      .

    log "Pushing image..."
    docker push "${ARTIFACT_REG}/${SERVICE_NAME}:${SHORT_SHA}" || error "Docker push failed for tag ${SHORT_SHA}"
    docker push "${ARTIFACT_REG}/${SERVICE_NAME}:latest" || error "Docker push failed for tag latest"
fi

# --- Service-Specific Deployment ---
log "Starting Cloud Run deployment for ${SERVICE_NAME}..."
case $SERVICE_NAME in
    "identity-service")
        deploy_service identity-service IdentityDB --no-allow-unauthenticated
        ;;
    "property-service")
        IDENTITY_URL=$(get_url identity-service)
        deploy_service property-service PropertyDB --no-allow-unauthenticated --set-env-vars="Identity__BaseUrl=${IDENTITY_URL}"
        ;;
    "crm-service")
        IDENTITY_URL=$(get_url identity-service)
        deploy_service crm-service CRMDB --no-allow-unauthenticated --set-env-vars="Identity__BaseUrl=${IDENTITY_URL}"
        ;;
    "developer-api")
        deploy_service developer-api DeveloperDB --no-allow-unauthenticated \
          --set-env-vars="ConnectionStrings__PropertyConnection=Server=127.0.0.1;Database=PropertyDB;User Id=sqlserver;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
        ;;
    "gateway")
        gcloud run deploy gateway --image="${ARTIFACT_REG}/gateway:${SHORT_SHA}" \
          --region="${REGION}" --service-account="gateway-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
          --allow-unauthenticated --min-instances=0 --max-instances=20 --cpu=2 --memory=2Gi \
          --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Cors__AllowedOrigins__0=https://app.${DOMAIN},Cors__AllowedOrigins__1=https://www.${DOMAIN}" --quiet
        ;;
    "frontend-client")
        GATEWAY_URL=$(get_url gateway)
        gcloud run deploy frontend-client --image="${ARTIFACT_REG}/frontend-client:${SHORT_SHA}" \
          --region="${REGION}" --service-account="frontend-client-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
          --allow-unauthenticated --set-env-vars="VITE_API_BASE_URL=${GATEWAY_URL},NODE_ENV=production" --quiet
        ;;
    "client-facing")
        GATEWAY_URL=$(get_url gateway)
        IDENTITY_URL=$(get_url identity-service)
        gcloud run deploy client-facing --image="${ARTIFACT_REG}/client-facing:${SHORT_SHA}" \
          --region="${REGION}" --service-account="client-facing-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
          --allow-unauthenticated --set-env-vars="NODE_ENV=production,NEXT_PUBLIC_GATEWAY_URL=${GATEWAY_URL},NEXT_PUBLIC_APP_URL=https://app.${DOMAIN},NEXT_PUBLIC_SITE_URL=https://www.${DOMAIN},LEAD_API_URL=${IDENTITY_URL}" --quiet
        ;;
    "brochure-worker")
        GATEWAY_URL=$(get_url gateway)
        deploy_worker brochure-worker --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Gateway__BaseUrl=${GATEWAY_URL},Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}"
        ;;
    "image-worker")
        deploy_worker image-worker --set-env-vars="Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}"
        ;;
    "syndication-worker")
        deploy_worker syndication-worker --set-env-vars="Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}"
        ;;
    *)
        deploy_service "${SERVICE_NAME}" "None" --no-allow-unauthenticated
        ;;
esac

log "Successfully deployed ${SERVICE_NAME}!"