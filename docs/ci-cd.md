# Deploy from main with GitHub Actions

Open the repository's **Actions → CI and deploy** page. Pull requests targeting `main` run checks. A push or merge to `main` checks the affected components without deploying. Production deployment happens only through **Run workflow**. Documentation-only commits skip deployment. A shared project change also selects the services referencing it, including transitive references.

The production workflow is `.github/workflows/ci-cd.yml`. Component paths, build settings and deployment targets are in `scripts/ci/services.json`. The Land Administration components are `frontend`, `gateway`, `identity`, `land`, `payments` and `reporting`. Existing database, email, payment and signing secrets remain in Google Cloud/Kubernetes; this workflow updates images, not secret values or infrastructure. Gateway releases also update the existing routes ConfigMap.

## Deploy one component manually

Choose **Run workflow**, select **main**, and pick the component. Use `changed` to include changes since the last successful complete manual main release, or `all` for a complete release. Other branches can run checks but cannot deploy production.

The workflow compares against the last successful complete manual release, so a failed release remains eligible on the next push. Releases are serialised, not cancelled halfway through; a stale queued commit fails before deployment if main has advanced, so it cannot become a successful release baseline. Images are tagged with the commit SHA, run ID and attempt. All selected images build before runtime changes begin. A failed rollout or health check restores that component's previous image/traffic. Database schema changes are not reversed by an image rollback. Earlier successfully deployed components in the same run remain deployed.

## Authentication and permissions

Google [Workload Identity Federation](https://github.com/google-github-actions/auth) exchanges GitHub's identity for a short-lived deployment token. No service account JSON key is stored in GitHub. Production access is restricted to the two Biscove repository IDs, their owner ID and `refs/heads/main`.

Repository **Settings → Secrets and variables → Actions → Variables**:

| Variable | Real estate value |
| --- | --- |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | `projects/381946570487/locations/global/workloadIdentityPools/biscove-github/providers/main-releases` |
| `GCP_DEPLOY_SERVICE_ACCOUNT` | `realestate-github-deploy@project-ff46407b-1a09-4376-95a.iam.gserviceaccount.com` |

The deployment account needs Artifact Registry writer on `biscove-land`, cluster viewer in the project, and the namespaced `github-deployer` Kubernetes role. It does not receive Secret Manager accessor or database administrator access. The `production` GitHub environment allows only main deployments. Temporary `gha-creds-*.json` files are excluded from Git and Docker contexts.

The real estate repository has the same workflow with its own deployment account, `realestate-github-deploy@project-ff46407b-1a09-4376-95a.iam.gserviceaccount.com`. Its account has writer access to `realestate-images`, Cloud Run developer on the six existing services, and service-account-user access to their runtime identities. Its additional `GOOGLE_MAPS_API_KEY` Actions variable is the existing public browser key; retain its API/referrer restrictions. Developer API work is outside that release until it is completed and explicitly added to `services.json`.

## Checks and troubleshooting

The first job tests deployment scope; the verification matrix builds each affected frontend/.NET service and runs relevant existing tests. The release job publishes containers, waits for runtime readiness and performs available health checks. Each deployed image appears in the Actions run summary.

To test selection locally, run `python3 scripts/ci/test_pipeline.py` and `python3 scripts/ci/pipeline.py select --base COMMIT`. A manual release from a non-main branch fails the main-branch guard. Changing pipeline logic triggers checks without automatically replacing runtime images; changing a Dockerfile or Docker context exclusions selects its consumers.

If cloud authentication fails, check the two repository variables and the workload identity provider's main/repository condition. If container push or rollout fails, the Actions log identifies the component; rerun the failed workflow or manually select that component. Keep deployment permissions scoped to the existing resources. Provisioning a new service, modifying a tenant's settings, changing fees/secrets or applying infrastructure remains a separate operation.
