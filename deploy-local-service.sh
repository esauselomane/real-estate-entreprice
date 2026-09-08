#!/bin/bash
set -e

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <service-name>"
    exit 1
fi

SERVICE=$1

echo "🔨 Building ${SERVICE}..."
docker compose -f docker-compose.prod.yml build "${SERVICE}"

echo "▶️  Restarting ${SERVICE}..."
docker compose -f docker-compose.prod.yml up -d "${SERVICE}"
