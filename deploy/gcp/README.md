# Google Cloud Deployment Guide

Johannesburg (`africa-south1`) is the default region. The existing `realestate-sql` instance in `us-central1` cannot be moved; this script creates a **new** regional SQL Server instance and empty databases. Local databases are not touched.

## Quick Start

```bash
gcloud auth login
gcloud auth application-default login

export PROJECT_ID="project-ff46407b-1a09-4376-95a"
export REGION="africa-south1"
./deploy/gcp/deploy.sh
```

Default public hosts (`DOMAIN=biscovetech.com`):

- `https://app.biscovetech.com` — admin UI (`frontend-client`)
- `https://api.biscovetech.com` — API gateway
- `https://www.biscovetech.com` — marketing site (`client-facing`)

Set `SKIP_DOMAIN_MAPPING=1` to skip Cloud Run domain mappings and bake `*.run.app` URLs instead.

The script:

- Enables required APIs
- Creates Artifact Registry in `africa-south1` (the US registry is not reused)
- Reuses `jwt-secret` and `mssql-password` when they already exist (does not rotate them)
- Creates `realestate-sql-africa-south1` with private IP and empty Identity/Property/CRM/Developer databases
- Stores regional connection-string secrets (`*-db-conn-africa-south1`)
- Builds and deploys Identity, Property, CRM, Developer API, Gateway, frontend-client, and client-facing
- Wires the gateway with `DOWNSTREAM_SCHEME=https` and Cloud Run hostnames
- Rebuilds the frontends **after** the gateway URL is known (Vite/Next public URLs are bake-time)

Kafka worker containers are **not** deployed. Image processing, syndication, brochure generation, and imports run as hosted services inside Property and CRM (`min-instances=1`, CPU always allocated).

## Required environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PROJECT_ID` | — | Required GCP project |
| `REGION` | `africa-south1` | Cloud Run, Artifact Registry, and Cloud SQL region |
| `ZONE` | `africa-south1-a` | Cloud SQL zone |
| `SQL_INSTANCE` | `realestate-sql-${REGION}` | Must be a **new** name; `realestate-sql` already exists in us-central1 |
| `DOMAIN` | `biscovetech.com` | Base domain for `app.` / `www.` / `api.` mappings |
| `NETWORK` / `SUBNET` | `default` | VPC used for Direct VPC egress to Cloud SQL |
| `VITE_GOOGLE_MAPS_API_KEY` | unset | Optional frontend build arg |
| `SKIP_DOMAIN_MAPPING` | `0` | Set to `1` to skip Cloud Run domain mappings |

## What changed vs the old script

- Default region is Johannesburg, not `us-central1`
- SQL connection strings use the instance **private IP** (SQL Server cannot use the Cloud SQL Unix socket)
- Broken `Server=localhost,1433` values are gone; passwords are not printed
- Service accounts match Cloud Run names (`identity-service-sa`, not `identity-sa`)
- Gateway routing uses Cloud Run HTTPS hosts/ports instead of Docker Compose hostnames
- Frontend images are built with `VITE_API_BASE_URL` / `NEXT_PUBLIC_*` build args
- Backends stay reachable so Ocelot can proxy (app-level JWT still applies)

## After Johannesburg is healthy

1. Point DNS at the Cloud Run domain-mapping records:
   - `app.biscovetech.com` → frontend-client
   - `api.biscovetech.com` → gateway
   - `www.biscovetech.com` → client-facing
   Wait for managed certificates (~15 minutes after DNS propagates).
2. Confirm `/health` on gateway, identity, and property.
3. Only then delete the **us-central1** Cloud Run services and `realestate-sql`. Do not run `destroy.sh` against us-central1 with the new defaults; set `REGION=us-central1` and `SQL_INSTANCE=realestate-sql` explicitly if you intend to tear that stack down.
4. Property documents currently write to container disk. Plan Cloud Storage before relying on the vault in production.

## Redeploy one service

```bash
export PROJECT_ID="project-ff46407b-1a09-4376-95a"
./deploy/gcp/deploy-service.sh property-service
```

## Files

- `deploy.sh` — provision + deploy
- `deploy-service.sh` — rebuild and replace one service
- `common.sh` — shared helpers
- `destroy.sh` — regional teardown (does not delete shared `jwt-secret` / `mssql-password`)
- `../../cloudbuild.yaml` — image builds for africa-south1; use `deploy.sh` for a full release (the Knative replace manifest still has placeholders)
