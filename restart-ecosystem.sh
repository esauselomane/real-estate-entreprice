#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

fail() { echo "ERROR: $*" >&2; exit 1; }
# macOS does not ship GNU timeout. Bound Docker calls as well as readiness checks.
bounded() {
  python3 - "$@" <<'PY'
import subprocess, sys
try:
    result = subprocess.run(sys.argv[2:], timeout=int(sys.argv[1]))
    sys.exit(result.returncode)
except subprocess.TimeoutExpired:
    print('Command timed out: ' + sys.argv[2], file=sys.stderr)
    sys.exit(124)
PY
}

for command in docker dotnet npm python3 curl lsof; do
  command -v "$command" >/dev/null || fail "Required command missing: $command"
done
python3 - "$ROOT_DIR" <<'CHECK_DISK' || fail "Free disk space before starting Docker and the applications (at least 1 GB required)."
import shutil, sys
free = shutil.disk_usage(sys.argv[1]).free
if free < 1024 ** 3:
    print(f"Only {free / 1024 ** 2:.0f} MB of disk space is available.", file=sys.stderr)
    sys.exit(1)
CHECK_DISK
bounded 15 docker info >/dev/null 2>&1 || fail "Docker is unavailable or unresponsive. Start/restart Docker Desktop, then retry. Existing app processes have not been stopped."

echo "Starting local infrastructure..."
bounded 180 docker compose up -d || fail "Infrastructure failed to start."
ready=false
for ((attempt=0; attempt<60; attempt++)); do
  if bounded 5 docker exec realestate-sqlserver-db sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -l 2 -t 2 -b -Q "SET NOCOUNT ON; SELECT 1"' >/dev/null 2>&1; then
    ready=true; break
  fi
  sleep 2
done
[ "$ready" = true ] || fail "SQL Server is not accepting queries. Inspect docker logs realestate-sqlserver-db."

echo "Building backend services sequentially..."
projects=(backend-services/Identity.Microservice backend-services/Property.Microservice backend-services/CRM.Microservice api-gateway)
names=(identity property crm gateway)
ports=(5001 5002 5003 5000)
for index in 0 1 2 3; do
  dotnet build "${projects[$index]}" -c Debug -m:1 --nologo >"/tmp/${names[$index]}-build.log" 2>&1 || fail "${names[$index]} build failed. See /tmp/${names[$index]}-build.log"
done
for frontend in frontend-client client-facing-app; do
  if [ ! -d "$frontend/node_modules" ]; then
    (cd "$frontend" && npm ci) || fail "Could not install dependencies for $frontend."
  fi
done

# Stop only listeners belonging to this checkout; never pkill every dotnet/node app.
for port in 3000 5000 5001 5002 5003 5173; do
  for pid in $(lsof -nP -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null | sort -u); do
    process_directory=$(lsof -a -p "$pid" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p')
    case "$process_directory" in
      "$ROOT_DIR"|"$ROOT_DIR"/*) kill "$pid" 2>/dev/null || true ;;
      *) fail "Port $port belongs to another application (PID $pid). Close it or change its port." ;;
    esac
  done
done
for ((attempt=0; attempt<20; attempt++)); do
  if ! lsof -nP -iTCP:3000 -iTCP:5000-5003 -iTCP:5173 -sTCP:LISTEN >/dev/null 2>&1; then break; fi
  sleep 1
done
if lsof -nP -iTCP:3000 -iTCP:5000-5003 -iTCP:5173 -sTCP:LISTEN >/dev/null 2>&1; then
  fail "An app did not release its port after shutdown. Inspect lsof before retrying."
fi

start_app() {
  local name="$1" directory="$2"
  shift 2
  (cd "$ROOT_DIR/$directory" && nohup "$@" >"/tmp/$name.log" 2>&1 </dev/null &)
}
wait_http() {
  local name="$1" url="$2"
  for ((attempt=0; attempt<90; attempt++)); do
    if curl --fail --silent --max-time 2 "$url" >/dev/null; then
      echo "Ready: $name ($url)"; return 0
    fi
    sleep 1
  done
  fail "$name did not become ready. See /tmp/$name.log."
}

echo "Starting APIs and their hosted background workers..."
for index in 0 1 2 3; do
  start_app "${names[$index]}" "${projects[$index]}" env ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development dotnet run --no-build --no-launch-profile -c Debug --urls "http://localhost:${ports[$index]}"
done
start_app vite frontend-client npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
start_app client_facing client-facing-app npm run dev -- --hostname 127.0.0.1 -p 3000
for index in 0 1 2 3; do
  wait_http "${names[$index]}" "http://localhost:${ports[$index]}/health"
done
wait_http vite http://localhost:5173
wait_http client_facing http://localhost:3000
echo "All application services are ready. Logs: /tmp/{identity,property,crm,gateway,vite,client_facing}.log"
