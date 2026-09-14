#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  Real Estate Enterprise — Google Cloud one-shot deployer
# ============================================================
#  Provisions:
#   - Artifact Registry
#   - Cloud SQL for SQL Server
#   - VPC + Serverless VPC connector
#   - Secret Manager secrets
#   - Service accounts + IAM roles
#   - Builds & pushes 10 container images
#   - Deploys 10 Cloud Run services
#
#  Usage:
#    export PROJECT_ID="my-realestate-prod"
#    export REGION="us-central1"
#    export DOMAIN="yourdomain.com"
#    ./deploy/gcp/deploy.sh
# ============================================================

# --- Configuration ---
PROJECT_ID="${PROJECT_ID:-your-gcp-project-id}"
REGION="${REGION:-us-central1}"
ZONE="${ZONE:-us-central1-a}"
DOMAIN="${DOMAIN:-your-production-domain.com}"
KAFKA_BOOTSTRAP_SERVERS="${KAFKA_BOOTSTRAP_SERVERS:-your-kafka-broker:9092}"
REPO_NAME="realestate-images"
SQL_INSTANCE="realestate-sql"
SQL_TIER="db-custom-1-3840"
SQL_PASSWORD="${SQL_PASSWORD:-$(openssl rand -base64 24 | tr -d /=+ | cut -c1-24)}"
JWT_SECRET="${JWT_SECRET:-$(openssl rand -base64 48 | tr -d /=+)}"
SHORT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo manual)"
ARTIFACT_REG="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO_NAME}"

# --- Color output ---
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
log()   { echo -e "${GREEN}[+]${NC} $*"; }
warn()  { echo -e "${YELLOW}[!]${NC} $*"; }
error() { echo -e "${RED}[-]${NC} $*" >&2; exit 1; }

# --- Pre-flight ---
command -v gcloud >/dev/null || error "gcloud CLI is required"
command -v docker  >/dev/null || error "docker is required"
gcloud config set project "${PROJECT_ID}" --quiet
log "Targeting project: ${PROJECT_ID} (region ${REGION})"

# --- Enable APIs ---
log "Enabling required Google Cloud APIs..."
gcloud services enable \
  run.googleapis.com \
  sqladmin.googleapis.com \
  secretmanager.googleapis.com \
  artifactregistry.googleapis.com \
  cloudbuild.googleapis.com \
  vpcaccess.googleapis.com \
  compute.googleapis.com \
  certificatemanager.googleapis.com \
  servicenetworking.googleapis.com \
  dns.googleapis.com \
  --quiet

# --- Artifact Registry ---
log "Creating Artifact Registry repository: ${REPO_NAME}"
gcloud artifacts repositories create "${REPO_NAME}" \
  --repository-format=docker \
  --location="${REGION}" \
  --description="Real Estate Enterprise container images" \
  --quiet 2>/dev/null || warn "Repository already exists"
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet

# --- Secrets ---
log "Storing secrets in Secret Manager..."
echo -n "${JWT_SECRET}" | gcloud secrets create jwt-secret --data-file=- --replication-policy=automatic --quiet 2>/dev/null || \
  echo -n "${JWT_SECRET}" | gcloud secrets versions add jwt-secret --data-file=- --quiet
echo -n "${SQL_PASSWORD}" | gcloud secrets create mssql-password --data-file=- --replication-policy=automatic --quiet 2>/dev/null || \
  echo -n "${SQL_PASSWORD}" | gcloud secrets versions add mssql-password --data-file=- --quiet
echo -n "support@${DOMAIN}" | gcloud secrets create support-email --data-file=- --replication-policy=automatic --quiet 2>/dev/null || true

# --- Cloud SQL ---
log "Configuring Private Services Access for Cloud SQL..."
gcloud compute addresses create google-managed-services-default \
  --global \
  --purpose=VPC_PEERING \
  --prefix-length=16 \
  --description="PEERING_ADDRESS_RANGE" \
  --network=default \
  --quiet 2>/dev/null || true

gcloud services vpc-peerings connect \
  --service=servicenetworking.googleapis.com \
  --ranges=google-managed-services-default \
  --network=default \
  --quiet 2>/dev/null || true

log "Creating Cloud SQL for SQL Server (this takes ~5 minutes)..."
gcloud sql instances describe "${SQL_INSTANCE}" >/dev/null 2>&1 || \
  gcloud sql instances create "${SQL_INSTANCE}" \
    --database-version=SQLSERVER_2022_EXPRESS \
    --tier="${SQL_TIER}" \
    --region="${REGION}" \
    --root-password="${SQL_PASSWORD}" \
    --availability-type=ZONAL \
    --network=projects/${PROJECT_ID}/global/networks/default \
    --no-assign-ip \
    --quiet

log "Creating databases..."
for db in IdentityDB PropertyDB CRMDB DeveloperDB; do
  gcloud sql databases create "${db}" --instance="${SQL_INSTANCE}" --quiet 2>/dev/null || warn "${db} exists"
done

# --- VPC Connector ---
log "Creating Serverless VPC connector..."
gcloud compute networks vpc-access connectors describe realestate-connector --region="${REGION}" >/dev/null 2>&1 || \
  gcloud compute networks vpc-access connectors create realestate-connector \
    --region="${REGION}" \
    --network=default \
    --range=10.8.0.0/28 \
    --min-instances=2 \
    --max-instances=10 \
    --quiet

# --- Service Accounts ---
log "Creating service accounts..."
for svc in identity property crm gateway frontend-client client-facing brochure-worker image-worker syndication-worker; do
  gcloud iam service-accounts describe "${svc}-sa@${PROJECT_ID}.iam.gserviceaccount.com" >/dev/null 2>&1 || \
    gcloud iam service-accounts create "${svc}-sa" \
      --display-name="Real Estate ${svc} service account" --quiet
done

# --- IAM ---
log "Binding IAM roles..."
for sa in identity property crm; do
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${sa}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="roles/cloudsql.client" --condition=None --quiet >/dev/null
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${sa}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="roles/secretmanager.secretAccessor" --condition=None --quiet >/dev/null
done

# Allow frontend service accounts to invoke other Cloud Run services
for sa in gateway frontend-client client-facing; do
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${sa}-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="roles/run.invoker" --condition=None --quiet >/dev/null
done

# --- Container builds ---
log "Building 10 container images in parallel..."
declare -A SERVICES=(
  ["identity-service"]="backend-services/Identity.Microservice/Dockerfile"
  ["property-service"]="backend-services/Property.Microservice/Dockerfile"
  ["crm-service"]="backend-services/CRM.Microservice/Dockerfile"
  ["gateway"]="api-gateway/Dockerfile"
  ["brochure-worker"]="backend-services/BrochureGeneration.Worker/Dockerfile"
  ["image-worker"]="backend-services/ImageProcessor.Worker/Dockerfile"
  ["syndication-worker"]="backend-services/PlatformSyndication.Worker/Dockerfile"
  ["frontend-client"]="frontend-client/Dockerfile"
  ["client-facing"]="client-facing-app/Dockerfile"
)

for svc in "${!SERVICES[@]}"; do
  dockerfile="${SERVICES[$svc]}"

  # Check if the specific tag already exists in registry to skip redundant build/push
  if gcloud artifacts docker images list "${ARTIFACT_REG}/${svc}" --filter="TAGS:${SHORT_SHA}" --format="get(package)" 2>/dev/null | grep -q "${svc}"; then
    log "Image ${svc}:${SHORT_SHA} already exists. Skipping build."
  else
    log "Building ${svc}..."
    docker build \
      --build-arg BUILDCONFIGURATION=Release \
      -t "${ARTIFACT_REG}/${svc}:${SHORT_SHA}" \
      -t "${ARTIFACT_REG}/${svc}:latest" \
      -f "${dockerfile}" \
      . >/tmp/build-${svc}.log 2>&1 || error "Build failed for ${svc} (see /tmp/build-${svc}.log)"

    docker push "${ARTIFACT_REG}/${svc}:${SHORT_SHA}" &
    docker push "${ARTIFACT_REG}/${svc}:latest" &
  fi
done
wait

# --- Cloud Run deployments ---
log "Deploying Cloud Run services..."

deploy_service() {
  local name=$1
  local db_name=$2
  shift 2
  log "  → ${name}"
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
    --set-env-vars="ConnectionStrings__DefaultConnection=Server=Server=localhost,1433,1433;Database=${db_name};User Id=sqlserver;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;,Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}" \
    --set-secrets="Jwt__Secret=jwt-secret:latest" \
    "$@" \
    --quiet
}

# Identity
deploy_service identity-service IdentityDB --no-allow-unauthenticated
IDENTITY_URL=$(gcloud run services describe identity-service --region="${REGION}" --format='value(status.url)')

# Property
deploy_service property-service PropertyDB --no-allow-unauthenticated --set-env-vars="Identity__BaseUrl=${IDENTITY_URL}"

# CRM
deploy_service crm-service CRMDB --no-allow-unauthenticated --set-env-vars="Identity__BaseUrl=${IDENTITY_URL}"

# Gateway (public)
log "  → gateway"
gcloud run deploy gateway --image="${ARTIFACT_REG}/gateway:${SHORT_SHA}" \
  --region="${REGION}" --service-account="gateway-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated --min-instances=0 --max-instances=20 --cpu=2 --memory=2Gi \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Cors__AllowedOrigins__0=https://app.${DOMAIN},Cors__AllowedOrigins__1=https://www.${DOMAIN}" --quiet

GATEWAY_URL=$(gcloud run services describe gateway --region="${REGION}" --format='value(status.url)')

# Frontend client (public)
gcloud run deploy frontend-client \
  --image="${ARTIFACT_REG}/frontend-client:${SHORT_SHA}" \
  --region="${REGION}" \
  --service-account="frontend-client-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --set-env-vars="VITE_API_BASE_URL=${GATEWAY_URL},NODE_ENV=production" \
  --min-instances=0 --max-instances=10 --cpu=2 --memory=2Gi --quiet

# Client-facing marketing (public)
gcloud run deploy client-facing \
  --image="${ARTIFACT_REG}/client-facing:${SHORT_SHA}" \
  --region="${REGION}" \
  --service-account="client-facing-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --set-env-vars="NODE_ENV=production,NEXT_PUBLIC_GATEWAY_URL=${GATEWAY_URL},NEXT_PUBLIC_APP_URL=https://app.${DOMAIN},NEXT_PUBLIC_SITE_URL=https://www.${DOMAIN},LEAD_API_URL=${IDENTITY_URL}" \
  --min-instances=0 --max-instances=10 --cpu=2 --memory=2Gi --quiet

# Workers (private)
gcloud run deploy brochure-worker \
  --image="${ARTIFACT_REG}/brochure-worker:${SHORT_SHA}" \
  --region="${REGION}" \
  --service-account="brochure-worker-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --vpc-connector=realestate-connector \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Gateway__BaseUrl=${GATEWAY_URL},Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}" \
  --min-instances=0 --max-instances=5 \
  --cpu=2 --memory=2Gi --timeout=3600 --quiet

gcloud run deploy image-worker \
  --image="${ARTIFACT_REG}/image-worker:${SHORT_SHA}" \
  --region="${REGION}" \
  --service-account="image-worker-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --vpc-connector=realestate-connector \
  --set-env-vars="Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}" \
  --min-instances=0 --max-instances=5 \
  --cpu=2 --memory=2Gi --timeout=3600 --quiet

gcloud run deploy syndication-worker \
  --image="${ARTIFACT_REG}/syndication-worker:${SHORT_SHA}" \
  --region="${REGION}" \
  --service-account="syndication-worker-sa@${PROJECT_ID}.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --vpc-connector=realestate-connector \
  --set-env-vars="Kafka__BootstrapServers=${KAFKA_BOOTSTRAP_SERVERS}" \
  --min-instances=0 --max-instances=5 \
  --cpu=2 --memory=2Gi --timeout=3600 --quiet

# --- Custom domains ---
log "Mapping custom domains..."
gcloud run domain-mappings create --service=frontend-client --domain="app.${DOMAIN}" --region="${REGION}" --quiet 2>/dev/null || warn "frontend-client domain mapping exists"
gcloud run domain-mappings create --service=client-facing --domain="www.${DOMAIN}" --region="${REGION}" --quiet 2>/dev/null || warn "client-facing domain mapping exists"
gcloud run domain-mappings create --service=gateway --domain="api.${DOMAIN}" --region="${REGION}" --quiet 2>/dev/null || warn "gateway domain mapping exists"

# --- IAM for unauthenticated access on gateway & frontends ---
for svc in gateway frontend-client client-facing; do
  gcloud run services add-iam-policy-binding "${svc}" \
    --region="${REGION}" \
    --member="allUsers" \
    --role="roles/run.invoker" \
    --condition=None \
    --quiet 2>/dev/null || true
done

# --- Summary ---
cat <<EOF

${GREEN}========================================${NC}
${GREEN}  DEPLOYMENT COMPLETE${NC}
${GREEN}========================================${NC}

Project:        ${PROJECT_ID}
Region:         ${REGION}
SQL Password:   ${SQL_PASSWORD}  (saved in Secret Manager)
JWT Secret:     ${JWT_SECRET:0:8}... (saved in Secret Manager)
Image Tag:      ${SHORT_SHA}

Service URLs:
  Identity:     ${IDENTITY_URL}
  Gateway:      ${GATEWAY_URL}
  Frontend:     $(gcloud run services describe frontend-client --region=${REGION} --format='value(status.url)' 2>/dev/null || echo pending)
  Marketing:    $(gcloud run services describe client-facing --region=${REGION} --format='value(status.url)' 2>/dev/null || echo pending)

Custom domains (configure DNS at your registrar):
  A  app.${DOMAIN}   → frontend-client
  A  www.${DOMAIN}   → client-facing
  A  api.${DOMAIN}   → gateway

${YELLOW}Manual follow-ups:${NC}
  1. Update DNS records (A or CNAME) for app/www/api.${DOMAIN}
  2. Wait for managed SSL certificates to provision (~15 min)
  3. Provision a Kafka cluster (Confluent Cloud or self-hosted) and update worker env vars
  4. Set up Cloud CDN for frontend-client service
  5. Configure Cloud Monitoring alerts (Uptime checks on /health)

${GREEN}Verify with:${NC}
  curl -i ${GATEWAY_URL}/health
  curl -i ${IDENTITY_URL}/health
EOF
