# Google Cloud Deployment Guide

This directory contains everything needed to deploy the Real Estate Enterprise platform to Google Cloud.

## Quick Start

```bash
# 1. Authenticate
gcloud auth login
gcloud config set project YOUR_PROJECT_ID

# 2. One-shot deploy (provisions all infrastructure)
export PROJECT_ID="my-realestate-prod"
export REGION="us-central1"
export DOMAIN="yourdomain.com"
./deploy/gcp/deploy.sh
```

The script will:
- Enable required APIs
- Create Artifact Registry repository
- Store secrets in Secret Manager
- Provision Cloud SQL for SQL Server
- Create VPC + Serverless VPC connector
- Create service accounts + IAM bindings
- Build 10 container images
- Deploy 10 Cloud Run services
- Map custom domains
- Print service URLs

## Estimated Cost (us-central1, low traffic)

| Resource | Config | Monthly |
|----------|--------|---------|
| Cloud SQL SQL Server Standard | db-custom-2-7680 (HA) | ~$280 |
| Identity Cloud Run | 1 vCPU, 1Gi, min=1, max=10 | ~$30 |
| Property Cloud Run | 1 vCPU, 1.5Gi, min=1, max=15 | ~$50 |
| CRM Cloud Run | 1 vCPU, 1.5Gi, min=1, max=15 | ~$50 |
| Gateway Cloud Run | 1 vCPU, 512Mi, min=1, max=20 | ~$25 |
| Frontend Cloud Run | 1 vCPU, 512Mi, min=1, max=10 | ~$25 |
| Client-Facing Cloud Run | 1 vCPU, 512Mi, min=1, max=10 | ~$25 |
| Workers (3) | 1 vCPU, 1Gi, min=0-1 | ~$20 |
| VPC Connector | 2-10 instances | ~$15 |
| **Total** | | **~$520/mo** |

Plus Cloud Storage for brochures, Memorystore Redis if you add caching, and Confluent Cloud for Kafka.

## Architecture

```
   ┌──────────────┐
   │ Cloud CDN    │ ← static assets
   └──────┬───────┘
          │
   ┌──────▼───────┐    ┌──────────────┐
   │ Frontend     │    │ Client-Facing│
   │ (admin UI)   │    │ (marketing)  │
   └──────┬───────┘    └──────┬───────┘
          │                   │
          └────────┬──────────┘
                   │
            ┌──────▼──────┐
            │  Gateway    │  (Ocelot, public)
            └──────┬──────┘
                   │
   ┌───────┬───────┼───────┬────────┐
   │       │       │       │        │
┌──▼──┐ ┌──▼──┐ ┌──▼──┐ ┌──▼──┐ ┌──▼──┐
│ID  │ │PROP │ │ CRM │ │DEV  │ │WORK │  (private)
└──┬──┘ └──┬──┘ └──┬──┘ └──┬──┘ └──┬──┘
   └──────┴───────┴──────┴──────┘
                  │
          ┌───────▼──────┐
          │  Cloud SQL   │ (private IP)
          └──────────────┘
```

## Required Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PROJECT_ID` | — | GCP project ID (required) |
| `REGION` | us-central1 | GCP region |
| `ZONE` | us-central1-a | GCP zone (for SQL HA pairing) |
| `DOMAIN` | yourdomain.com | Base domain for cloud run domain mappings |
| `SQL_PASSWORD` | auto-generated | SA password for Cloud SQL |
| `JWT_SECRET` | auto-generated | Symmetric JWT signing key |

## Manual Follow-ups After Deploy

1. **DNS** — Add A or CNAME records at your registrar:
   - `app.yourdomain.com` → frontend-client service
   - `www.yourdomain.com` → client-facing service
   - `api.yourdomain.com` → gateway service

2. **SSL certificates** — Wait for managed certs to provision (~15 min after DNS propagates).

3. **Kafka** — Cloud Run can't run a Kafka broker. Use Confluent Cloud, or run a dedicated VM/MIG with Kafka. Update worker env vars.

4. **Cloud SQL authorized networks** — Cloud Run uses the VPC connector, so no authorized network changes needed.

5. **Cloud CDN** — Enable on frontend-client for static asset caching:
   ```bash
   gcloud compute backend-services update ... --enable-cdn
   ```

6. **Cloud Monitoring alerts** — Set up uptime checks on `/health` endpoints.

## CI/CD

Connect Cloud Build to your GitHub repo:

```bash
gcloud builds triggers create github \
  --repo-name=real-estate-entreprice \
  --repo-owner=YOUR_GITHUB_USER \
  --branch-pattern="^main$" \
  --build-config=cloudbuild.yaml
```

After the first deploy, you can also run `cloudbuild.yaml` directly:
```bash
gcloud builds submit --config=cloudbuild.yaml
```

## Files in This Directory

- `deploy.sh` — One-shot provision + deploy (production)
- `destroy.sh` — Tear down all resources
- `cloud-run-manifest.yaml` — Service definitions (alternative to CLI)
- `ocelot.production.json` — Gateway routing for Cloud Run
- `../../cloudbuild.yaml` — Cloud Build pipeline

## Troubleshooting

**401 on gateway** — Verify JWT secret is the same in Identity, Property, CRM, Developer-API services (it is, all read from `jwt-secret:latest`).

**Cloud Run times out connecting to Cloud SQL** — Check VPC connector and ensure `Identity__BaseUrl` / `Gateway__BaseUrl` use the public Cloud Run URLs.

**Identity service can't be reached by Permission check** — Update the `Identity__BaseUrl` env var on Property and CRM services to point to the actual `identity-service-xxx.a.run.app` URL after first deploy.
