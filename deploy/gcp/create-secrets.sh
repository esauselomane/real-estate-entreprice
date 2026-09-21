#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

echo "Starting Secret Manager configuration..."

# Configuration
PROJECT_ID="${PROJECT_ID:-$(gcloud config get-value project)}"
DOMAIN="${DOMAIN:-biscovetech.com}"

if [ -z "$PROJECT_ID" ]; then
  echo "Error: PROJECT_ID is not set. Please set it or run 'gcloud config set project'."
  exit 1
fi

# Ensure openssl is available for random generation
if ! command -v openssl >/dev/null 2>&1; then
  echo "Error: openssl is required for secret generation."
  exit 1
fi

# Generate random values for new setups if not provided in environment
JWT_SECRET="${JWT_SECRET:-$(openssl rand -base64 48 | tr -d '/+=' )}"
MSSQL_PASSWORD="${MSSQL_PASSWORD:-$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-24)}"

# Array of secret keys and values
declare -A SECRETS=(
  ["NEXT_PUBLIC_GATEWAY_URL"]="https://api.${DOMAIN}"
  ["NEXT_PUBLIC_APP_URL"]="https://app.${DOMAIN}"
  ["NEXT_PUBLIC_SITE_URL"]="https://www.${DOMAIN}"
  ["LEAD_API_URL"]="https://api.${DOMAIN}/gateway/system/lead-requests"
  ["jwt-secret"]="$JWT_SECRET"
  ["mssql-password"]="$MSSQL_PASSWORD"
  ["support-email"]="admin@${DOMAIN}"
  ["google-maps-api-key"]="${VITE_GOOGLE_MAPS_API_KEY:-AIzaSyA_ISwPvfaEwCvFl0Q3yCneBa6s_sLuYBk}"
)

echo "Targeting project: $PROJECT_ID"
echo "Base domain: $DOMAIN"

# Loop through and create each secret
for SECRET_NAME in "${!SECRETS[@]}"; do
  SECRET_VALUE="${SECRETS[$SECRET_NAME]}"
  
  echo "----------------------------------------"
  
  if [ -z "$SECRET_VALUE" ]; then
    echo "Skipping '$SECRET_NAME' because value is empty."
    continue
  fi

  # Check if the secret already exists to avoid errors
  if gcloud secrets describe "$SECRET_NAME" --project="$PROJECT_ID" >/dev/null 2>&1; then
    echo "Secret '$SECRET_NAME' already exists. Adding a new version..."
  else
    echo "Creating new secret '$SECRET_NAME'..."
    gcloud secrets create "$SECRET_NAME" --replication-policy="automatic" --project="$PROJECT_ID"
  fi

  # Add the secret value as a new version
  echo -n "$SECRET_VALUE" | gcloud secrets versions add "$SECRET_NAME" --data-file=- --project="$PROJECT_ID"
  echo "Successfully updated '$SECRET_NAME'."
done

echo "----------------------------------------"
echo "All secrets successfully configured in Google Cloud Secret Manager!"
