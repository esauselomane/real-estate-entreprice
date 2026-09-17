#!/usr/bin/env bash
# Shared helpers for Google Cloud deploy scripts. Sourced, not executed.

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() {
  echo -e "${GREEN}[+]${NC} $*"
}

warn() {
  echo -e "${YELLOW}[!]${NC} $*"
}

error() {
  echo -e "${RED}[-]${NC} $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null || error "$1 is required"
}

host_from_url() {
  echo "$1" | sed -E 's#^https?://##' | cut -d/ -f1
}

secret_exists() {
  gcloud secrets describe "$1" --project="${PROJECT_ID}" >/dev/null 2>&1
}

write_secret() {
  local secret_name=$1
  local secret_value=$2
  local temp_file
  temp_file="$(mktemp)"
  printf '%s' "${secret_value}" > "${temp_file}"
  if secret_exists "${secret_name}"; then
    gcloud secrets versions add "${secret_name}" \
      --data-file="${temp_file}" \
      --project="${PROJECT_ID}" \
      --quiet >/dev/null
  else
    gcloud secrets create "${secret_name}" \
      --data-file="${temp_file}" \
      --replication-policy=automatic \
      --project="${PROJECT_ID}" \
      --quiet >/dev/null
  fi
  rm -f "${temp_file}"
}

ensure_secret_if_missing() {
  local secret_name=$1
  local secret_value=$2
  if secret_exists "${secret_name}"; then
    log "Reusing existing secret ${secret_name}"
    return 0
  fi
  write_secret "${secret_name}" "${secret_value}"
  log "Created secret ${secret_name}"
}

read_secret() {
  gcloud secrets versions access latest --secret="$1" --project="${PROJECT_ID}"
}

sql_connection_string() {
  local database_name=$1
  printf 'Server=%s,1433;Database=%s;User Id=sqlserver;Password=%s;TrustServerCertificate=True;Encrypt=True;' \
    "${SQL_PRIVATE_IP}" \
    "${database_name}" \
    "${SQL_PASSWORD}"
}

artifact_image() {
  local service_name=$1
  echo "${ARTIFACT_REG}/${service_name}:${SHORT_SHA}"
}

image_tag_exists() {
  local service_name=$1
  gcloud artifacts docker images list "${ARTIFACT_REG}/${service_name}" \
    --include-tags \
    --filter="tags:${SHORT_SHA}" \
    --format="value(package)" \
    --project="${PROJECT_ID}" 2>/dev/null | grep -q "${service_name}"
}

build_and_push_image() {
  local service_name=$1
  local dockerfile=$2
  shift 2

  if [ "${FORCE_REBUILD:-0}" != "1" ] && image_tag_exists "${service_name}"; then
    log "Image ${service_name}:${SHORT_SHA} already exists. Skipping build."
    return 0
  fi

  log "Building ${service_name} via Google Cloud Build..."
  # Cloud Build handles authentication internally and is more reliable than local Docker.
  local build_args_json=""
  if [ $# -gt 0 ]; then
    # Prepare build-args for the Cloud Build config
    build_args_json=$(printf "'%s', " "$@")
  fi

  gcloud builds submit . \
    --project="${PROJECT_ID}" \
    --config <(cat <<EOF
steps:
- name: 'gcr.io/cloud-builders/docker'
  args: ['build', 
         '--build-arg', 'BUILD_CONFIGURATION=Release',
         '--build-arg', 'BUILDCONFIGURATION=Release',
         ${build_args_json}
         '-t', '$(artifact_image "${service_name}")',
         '-f', '${dockerfile}', 
         '.']
images:
- '$(artifact_image "${service_name}")'
EOF
) --quiet || error "Cloud Build failed for ${service_name}"

  # Add the 'latest' tag to the image in Artifact Registry
  gcloud artifacts docker tags add "$(artifact_image "${service_name}")" \
    "${ARTIFACT_REG}/${service_name}:latest" --project="${PROJECT_ID}" --quiet
}

run_url() {
  gcloud run services describe "$1" \
    --region="${REGION}" \
    --project="${PROJECT_ID}" \
    --format='value(status.url)' 2>/dev/null || echo ""
}

grant_secret_accessor() {
  local service_account_email=$1
  local secret_name=$2
  gcloud secrets add-iam-policy-binding "${secret_name}" \
    --member="serviceAccount:${service_account_email}" \
    --role="roles/secretmanager.secretAccessor" \
    --project="${PROJECT_ID}" \
    --quiet >/dev/null
}

ensure_service_account() {
  local account_id=$1
  local display_name=$2
  gcloud iam service-accounts describe "${account_id}@${PROJECT_ID}.iam.gserviceaccount.com" \
    --project="${PROJECT_ID}" >/dev/null 2>&1 || \
    gcloud iam service-accounts create "${account_id}" \
      --display-name="${display_name}" \
      --project="${PROJECT_ID}" \
      --quiet
}

append_env_var() {
  local file_path=$1
  local key=$2
  local value=$3
  local escaped
  escaped="$(printf '%s' "${value}" | sed 's/\\/\\\\/g; s/"/\\"/g')"
  printf '%s: "%s"\n' "${key}" "${escaped}" >> "${file_path}"
}

sql_private_ip() {
  gcloud sql instances list \
    --project="${PROJECT_ID}" \
    --filter="name=${SQL_INSTANCE}" \
    --format='value(PRIVATE_ADDRESS)'
}

deploy_gateway() {
  local cors_origins=$1
  local gateway_base=$2
  local identity_host=$3
  local property_host=$4
  local crm_host=$5
  local project_id=$6
  local region=$7
  local network=$8
  local subnet=$9
  local artifact_image_name=${10}

  local gateway_env
  gateway_env="$(mktemp)"
  append_env_var "${gateway_env}" ASPNETCORE_ENVIRONMENT Production
  append_env_var "${gateway_env}" DOWNSTREAM_SCHEME https
  append_env_var "${gateway_env}" IDENTITY_HOST "${identity_host}"
  append_env_var "${gateway_env}" IDENTITY_PORT 443
  append_env_var "${gateway_env}" PROPERTY_HOST "${property_host}"
  append_env_var "${gateway_env}" PROPERTY_PORT 443
  append_env_var "${gateway_env}" CRM_HOST "${crm_host}"
  append_env_var "${gateway_env}" CRM_PORT 443
  append_env_var "${gateway_env}" Gateway__BaseUrl "${gateway_base}"
  append_env_var "${gateway_env}" CORS_ORIGINS "${cors_origins}"
  append_env_var "${gateway_env}" Cors__AllowedOrigins "${cors_origins}"
  append_env_var "${gateway_env}" AllowedOrigins "${cors_origins}"
  append_env_var "${gateway_env}" ASPNETCORE_URLS "http://*:5000"

  log "Deploying gateway..."
  gcloud run services update gateway --clear-vpc-connector --region="${region}" --project="${project_id}" --quiet 2>/dev/null || true
  gcloud run deploy gateway \
    --image="${artifact_image_name}" \
    --region="${region}" \
    --project="${project_id}" \
    --platform=managed \
    --service-account="gateway-sa@${project_id}.iam.gserviceaccount.com" \
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
