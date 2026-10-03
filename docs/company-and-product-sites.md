# Company and product presentation

`client-facing-app` is the Biscove Technologies company website. Home, About, Mission and Contact describe the company; `/solutions` introduces Biscove Real Estate and Biscove Land Administration.

`frontend-client` shows the real estate product landing page to signed-out visitors, including features, live pricing, trial links and the existing sign-in form. Signed-in visitors still enter the workspace. Activation and recovery query parameters retain priority over the landing page. Legacy company `/pricing` and `/login` URLs direct visitors to the relevant product section.

## Public URLs

- Marketing `NEXT_PUBLIC_APP_URL`: real estate application URL (existing setting).
- Marketing `NEXT_PUBLIC_LAND_APP_URL`: optional land application deployment. When absent, use the company Contact page rather than invent a production address.
- Real estate `VITE_COMPANY_SITE_URL`: company website URL; defaults to `https://www.biscovetech.com`. Set to `http://localhost:3000` for local trials and contact links.
- Real estate `VITE_API_BASE_URL`: existing gateway base ending in `/gateway`; pricing reads `/system/lead-requests/pricing-packages`.

The existing marketing onboarding and lead APIs are retained. Trial enrolment is explicitly for Biscove Real Estate. The production marketing build links Land Administration to `https://landadmin.biscovetech.com`; development builds can retain the Contact fallback. Live prices remain authoritative; no fallback prices or unsupported customer claims were added.


## Deploy the websites to Johannesburg

Use `deploy/gcp/cloudbuild-sites.yaml` to build just the two public interfaces with their production URLs. Supply `_GOOGLE_MAPS_API_KEY` with the existing browser key; retain its API and referrer restrictions. The image tag should identify the committed release. Submit from a clean source archive containing `client-facing-app`, `frontend-client`, this build configuration and `.dockerignore`, so local backend work, credentials, dependencies and build outputs stay out of the upload.

Deploy each image with `gcloud run services update SERVICE --image=IMAGE --region=africa-south1 --project=project-ff46407b-1a09-4376-95a`. This retains the existing environment, secret references, service identity, networking, domain mappings and resource settings. Check the new ready revision and public sites after updating; restore the previous revision using `gcloud run services update-traffic` if validation fails.

The Developer API work remains local and is not part of this website release.


## Verified production release — 3 October 2026

- Source commit: `2d75cc4d4f51` on the existing `remove-kafka` branch, pushed to GitHub.
- Regional Cloud Build: `8e19dc98-82df-4d54-b6b7-dfd32f29c888`, `africa-south1`, successful. Image tag: `sites-2d75cc4d4f51`.
- Company site: `https://www.biscovetech.com`, Cloud Run revision `client-facing-00016-pzq`, serving 100% of traffic. Previous revision: `client-facing-00015-hbs`.
- Real estate app: `https://app.biscovetech.com`, Cloud Run revision `frontend-client-00015-blc`, serving 100% of traffic. Previous revision: `frontend-client-00014-vcl`.
- Land Administration link: `https://landadmin.biscovetech.com`.
- Passed: both production builds, new landing/mobile component lint checks, public company/solutions content, live pricing catalogue, sign-in redirect, recovery/activation routing, mobile landing widths, and JavaScript runtime checks.
- Browser-only mock data verified the deployed workspace navigation, keyboard focus/Escape behavior, role-filtered menus, administration screens and 320–600px layouts without changing production records. This is UI verification; authenticated backend behavior was not changed or retested with these fixtures.
- Gateway, Identity, Property and CRM revisions were preserved. Developer API edits and local runtime/build artifacts remain outside the release.
