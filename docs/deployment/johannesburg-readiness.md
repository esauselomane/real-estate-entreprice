# Johannesburg release readiness

Deployment has not started. The user requested notification before starting deployment.

## Authorized scope

Replace the existing cloud application databases with fresh databases in africa-south1, without migrating records. Preserve local databases. Reuse existing project settings/secrets where suitable. Build the application from an exact main-branch commit. Tenant/user/contact consistency remains deferred.

## Read-only inventory

Project: project-ff46407b-1a09-4376-95a.

Existing SQL instance: realestate-sql, us-central1, SQLSERVER_2022_EXPRESS, db-custom-1-3840, ZONAL.
Application databases: IdentityDB, PropertyDB, CRMDB, DeveloperDB. Do not treat master, tempdb, model or msdb as application reset targets.

Existing secret names (values were not retrieved): crm-connection-string, crm-service-db-conn, database-connection-string, developer-api-db-conn, identity-service-db-conn, jwt-secret, mssql-password, property-connection-string, property-service-db-conn, support-email. All listed secrets use automatic replication. No separate PayFast, SMTP or Google Maps credential secrets were listed.

Johannesburg supports Cloud Run and Cloud SQL for SQL Server:
- https://docs.cloud.google.com/run/docs/locations
- https://docs.cloud.google.com/sql/docs/sqlserver/region-availability-overview

SQL sizing and network compatibility still need validation for the replacement instance. Region changes require a new instance, not an in-place location edit. Reusing global secrets does not guarantee regional-only secret storage.

## Source state

Current working branch: remove-kafka, HEAD 61a64a6. Fetched origin/main: 31e8575. Current HEAD is an ancestor of origin/main (3 merge/history commits behind); their committed trees currently match. Many implementation changes remain uncommitted. Do not build the old main commit and claim it includes these changes.

Before building: review changes, exclude local uploads/artifacts/credentials from the release, integrate onto main and record the exact release commit. No merge or push has been performed in this preparation pass.

## Fixes in this preparation pass

Frontend Dockerfile accepts VITE_API_BASE_URL at build time, alongside VITE_GOOGLE_MAPS_API_KEY.
Marketing Dockerfile accepts NEXT_PUBLIC_GATEWAY_URL, NEXT_PUBLIC_APP_URL and NEXT_PUBLIC_SITE_URL at build time.
Runtime-only settings cannot replace values embedded in browser bundles.

## Remaining release gates

1. Fresh Identity startup currently creates many demo users using a shared password; production bootstrap must separate demo data from required roles/pricing. An initial administrator email has been requested; generate and store its initial credential securely, using the existing password hashing implementation. Do not publish demo credentials.
2. Review Property demo seeding and ensure a fresh business database has intended reference data, not invented listings. Check clean-database migrations for all four databases.
3. Create missing credential secrets or map suitable existing values without printing them. Update connection-string secrets only when replacement endpoints exist; avoid breaking the old environment prematurely.
4. Prepare regional networking, SQL connectivity, Cloud Run service identities, HTTPS downstream hosts/ports, CORS and frontend URLs. Existing shell deploy scripts contain stale connection strings and must not be run unchanged.
5. Ensure the build source bundle excludes local .env files and credentials in tracked appsettings. Current Dockerfiles copy appsettings into images; runtime overrides do not remove embedded secrets.
6. Verify Google API enablement/referrer restrictions and real address selection. Automated address tests simulate Google responses.
7. Deploy payment-capable Identity and gateway revisions before real PayFast acceptance; both deployed endpoints previously returned 404. Keep PayFast sandbox enabled until acceptance.
8. Notify the user with the exact commit, regional resource plan and deletion targets before any deployment or deletion.

No cloud resources, secret values or databases were modified during these checks.
