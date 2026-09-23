#!/bin/bash

# deploy-service.sh
# Usage: ./deploy-service.sh <service-name> [--drop-db]

SERVICE_NAME=$1
DROP_DB_FLAG=false

# Parse arguments to find the --drop-db flag
for arg in "$@"; do
  if [ "$arg" == "--drop-db" ]; then
    DROP_DB_FLAG=true
  fi
done

if [ -z "$SERVICE_NAME" ]; then
  echo "Error: No service name provided."
  echo "Usage: ./deploy-service.sh <service-name> [--drop-db]"
  exit 1
fi

# Load environment variables from .env to access MSSQL_SA_PASSWORD and CRM_DB
if [ ! -f .env ]; then
  echo "Error: .env file not found."
  exit 1
fi

export $(grep -v '^#' .env | xargs)

# Database maintenance logic
# Note: Matches 'crm' or 'crm-service' per your docker-compose configuration
if [[ "$SERVICE_NAME" =~ ^crm ]] && [ "$DROP_DB_FLAG" == "true" ]; then
  TARGET_DB=${CRM_DB:-CRMDb}
  echo "⚠️  --drop-db flag detected. Resetting database: $TARGET_DB"

  # Execute SQL command inside the running sqlserver container with error handling
  if ! docker exec -t realestate-sqlserver /opt/mssql-tools/bin/sqlcmd \
      -S localhost -U sa -P "$MSSQL_SA_PASSWORD" \
      -Q "IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$TARGET_DB')
          BEGIN
            ALTER DATABASE [$TARGET_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [$TARGET_DB];
          END;
          CREATE DATABASE [$TARGET_DB];"; then
    echo "❌ Error: Failed to reset database $TARGET_DB."
    exit 1
  fi
fi

# Deploy the specified service
docker-compose -f docker-compose.prod.yml up -d --build "$SERVICE_NAME"

# Ensure migrations are run by waiting for the service to become healthy.
# The services in this project execute migration logic during their startup phase.
echo "🚀 $SERVICE_NAME deployed. Waiting for health check to confirm successful initialization and migrations..."

# Map compose service name to the specific container name used in docker-compose.prod.yml
case "$SERVICE_NAME" in
  identity-service) CONTAINER_NAME="realestate-identity" ;;
  property-service) CONTAINER_NAME="realestate-property" ;;
  crm-service)      CONTAINER_NAME="realestate-crm" ;;
  gateway)          CONTAINER_NAME="realestate-gateway" ;;
  *)                CONTAINER_NAME="" ;;
esac

if [ -n "$CONTAINER_NAME" ]; then
  MAX_WAIT=18 # Total wait time: 3 minutes (18 * 10s)
  COUNTER=0
  while [ $COUNTER -lt $MAX_WAIT ]; do
    STATUS=$(docker inspect --format='{{.State.Health.Status}}' "$CONTAINER_NAME" 2>/dev/null || echo "starting")
    if [ "$STATUS" == "healthy" ]; then
      echo "✅ $SERVICE_NAME is healthy. Migrations confirmed."
      exit 0
    elif [ "$STATUS" == "unhealthy" ]; then
      echo "❌ $SERVICE_NAME is unhealthy. Migration or startup failed. Tailing last 20 lines of logs:"
      docker logs "$CONTAINER_NAME" | tail -n 20
      exit 1
    fi
    echo "⏳ $SERVICE_NAME Status: $STATUS... $(( (COUNTER+1) * 10 ))s elapsed"
    sleep 10
    COUNTER=$((COUNTER + 1))
  done
  echo "⚠️  Timeout waiting for $SERVICE_NAME to become healthy. Check 'docker logs $CONTAINER_NAME' for details."
fi
