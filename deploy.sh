#!/bin/bash
set -e

echo "🚀 Deploying Biscove Real Estate Tech Platform..."

# Check if .env file exists
if [ ! -f .env ]; then
    echo "⚠️  .env file not found. Creating from .env.example..."
    cp .env.example .env
    echo "📝 Please edit .env with your production values before continuing."
    echo "   Especially: MSSQL_SA_PASSWORD, JWT_SECRET, CORS_ORIGINS"
    exit 1
fi

# Build all services
echo "🔨 Building Docker images..."
docker compose -f docker-compose.prod.yml build --no-cache

# Start services
echo "▶️  Starting services..."
docker compose -f docker-compose.prod.yml up -d

# Wait for SQL Server
echo "⏳ Waiting for SQL Server to be ready..."
sleep 30

# Show service status
echo "📊 Service status:"
docker compose -f docker-compose.prod.yml ps

echo ""
echo "✅ Deployment complete!"
echo "🌐 Client-facing app: http://localhost:${CLIENT_FACING_PORT:-80}"
echo "🖥️  Admin portal: http://localhost:${FRONTEND_CLIENT_PORT:-3000}"
echo "🔌 API Gateway: http://localhost:${GATEWAY_PORT:-5000}"
echo ""
echo "📋 Useful commands:"
echo "  docker compose -f docker-compose.prod.yml logs -f [service]    # View logs"
echo "  docker compose -f docker-compose.prod.yml ps                  # List services"
echo "  docker compose -f docker-compose.prod.yml down                # Stop all services"
