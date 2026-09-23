# Local startup

Run `./restart-ecosystem.sh` from any directory. Docker Desktop must be running,
.NET 10 and Node/npm installed, and at least 1 GB of disk space available (several
GB recommended for Docker growth and builds).

The script starts the Compose infrastructure, verifies SQL can execute a query,
builds the four APIs sequentially, then restarts this checkout's app listeners.
Background jobs are hosted inside Identity, Property and CRM; there are no standalone
worker projects to launch. ASP.NET runs in Development with Debug builds and loads
normal local user secrets. No secrets are written by the script.

Endpoints:
- Frontend: http://localhost:5173
- Client-facing app: http://localhost:3000
- Gateway: http://localhost:5000/health
- Identity: http://localhost:5001/health
- Property: http://localhost:5002/health
- CRM: http://localhost:5003/health

Logs are `/tmp/identity.log`, `/tmp/property.log`, `/tmp/crm.log`, `/tmp/gateway.log`,
`/tmp/vite.log`, `/tmp/client_facing.log`; API build logs use `-build.log` suffixes.
Startup exits nonzero if prerequisites, builds or readiness checks fail.
API health responses establish process readiness after startup migrations; they do
not continuously prove dependency health.

If Docker commands hang, check free disk space and restart Docker Desktop. Do not
reset Docker or remove database volumes to fix startup. The CRM broker metrics
migration tolerates tables created by older local bootstraps, preserving their data.
