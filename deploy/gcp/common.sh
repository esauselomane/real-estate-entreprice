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

  log "Building ${service_name}..."
  docker build \
    --build-arg BUILD_CONFIGURATION=Release \
    --build-arg BUILDCONFIGURATION=Release \
    "$@" \
    -t "$(artifact_image "${service_name}")" \
    -t "${ARTIFACT_REG}/${service_name}:latest" \
    -f "${dockerfile}" \
    . || error "Build failed for ${service_name}"

  docker push "$(artifact_image "${service_name}")" || error "Push failed for ${service_name}:${SHORT_SHA}"
  docker push "${ARTIFACT_REG}/${service_name}:latest" || error "Push failed for ${service_name}:latest"
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
  gcloud sql instances describe "${SQL_INSTANCE}" \
    --project="${PROJECT_ID}" \
    --format='value(ipAddresses[0].ipAddress)'
}
