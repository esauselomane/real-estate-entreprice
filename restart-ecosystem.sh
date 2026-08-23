#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

echo "===================================================="
echo "🔄 RESTARTING FULL REAL ESTATE ECOSYSTEM"
echo "===================================================="

echo "Stopping any processes still bound to app ports..."
for port in 5000 5001 5002 5003 5173 1433 9092 2181; do
  pids=$(lsof -ti tcp:"$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "  -> killing processes on port $port: $pids"
    kill -9 $pids 2>/dev/null || true
  fi
done

pkill -f 'dotnet run' || true
pkill -f 'vite' || true
pkill -f 'npm run dev' || true

if ! command -v docker >/dev/null 2>&1 || ! docker info >/dev/null 2>&1; then
  echo "Docker daemon is not running or Docker Desktop is not started."
  echo "Start Docker Desktop and rerun this script to bring up SQL Server, Kafka, and Zookeeper."
  exit 1
fi

echo "Starting infrastructure containers..."
docker compose up -d

for _ in {1..60}; do
  if python3 - <<'PY'
import socket
s = socket.socket()
s.settimeout(1)
try:
    s.connect(('127.0.0.1', 1433))
    print('ready')
except Exception:
    raise SystemExit(1)
finally:
    s.close()
PY
  then
    echo "SQL Server is ready."
    break
  fi
  sleep 2
done

if ! python3 - <<'PY'
import socket
s = socket.socket()
s.settimeout(1)
try:
    s.connect(('127.0.0.1', 1433))
except Exception:
    raise SystemExit(1)
finally:
    s.close()
PY
then
  echo "SQL Server did not become ready on localhost:1433."
  exit 1
fi

echo "Starting backend services..."
(cd backend-services/Identity.Microservice && nohup dotnet run --configuration Development --urls http://localhost:5001 >/tmp/identity.log 2>&1 &)
(cd backend-services/Property.Microservice && nohup dotnet run --configuration Development --urls http://localhost:5002 >/tmp/property.log 2>&1 &)
(cd backend-services/CRM.Microservice && nohup dotnet run --configuration Development --urls http://localhost:5003 >/tmp/crm.log 2>&1 &)
(cd api-gateway && nohup dotnet run --configuration Development --urls http://localhost:5000 >/tmp/gateway.log 2>&1 &)

echo "Starting frontend..."
(cd frontend-client && nohup npm run dev -- --host 0.0.0.0 >/tmp/vite.log 2>&1 &)

echo "Waiting for gateway and frontend to respond..."
started=false
for _ in {1..80}; do
  if curl -fsS http://localhost:5000 >/dev/null 2>&1 && curl -fsS http://localhost:5173 >/dev/null 2>&1; then
    started=true
    echo "✅ Gateway and frontend are live."
    break
  fi
  sleep 1
done

if [ "$started" = false ]; then
  echo "⚠️ The gateway or frontend did not become ready within the wait window."
  echo "Check the logs in /tmp/gateway.log, /tmp/property.log, /tmp/crm.log, and /tmp/identity.log."
fi

echo "--- gateway status ---"
curl -I http://localhost:5000 2>/dev/null || true

echo "--- frontend status ---"
curl -I http://localhost:5173 2>/dev/null || true

echo "===================================================="
echo "🎉 RESTART SCRIPT COMPLETE"
echo "===================================================="
