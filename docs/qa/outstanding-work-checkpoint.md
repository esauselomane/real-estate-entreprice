# Outstanding work checkpoint

## Verified 2026-09-15

Changes are saved in the working tree, not deployed or committed.

- Frontend production build passed. Identity, CRM, Property and Gateway builds passed.
- Backend regression suite: 36 passed, 0 failed. Existing compiler/dependency warnings remain.
- Local API checks passed on normal ports: Gateway 5000, Identity 5001, Property 5002, CRM 5003; frontend 5173.
- AgencyAdmin can view own agency and descendants; unrelated tenant overrides return no records. Broker cannot view agency-wide financial summary.
- Deal and contact updates passed with existing values. Profile updates preserve email, role and tenant despite attempted protected-field changes.
- Subagency creation forces the administrator's agency as parent and synchronizes the new tenant with Identity. Child-agency users appear in the managed roster; PlatformAdmin creation is denied.
- New broker provisioning now uses the returned Identity user ID and selected agency. Monthly targets use that broker's tenant; unrelated tenant filters return no targets. Live creation and target checks passed.
- Browser checks cover CRM Directory, populated Deals, exclusive sidebar expansion, profile modal and responsive billing layout. Screenshots are in artifacts/agency-qa.

## Changes completed

- Scoped operational CRM/property access and mutations, including tenant override intersection.
- Explicit agency selection for user creation, including the full agency roster.
- Named deal-stage/contact-type request contracts and optional relationship normalization.
- Centered native profile dialog with retry state; gateway profile and avatar routes added.
- Profile avatar storage uses the service webroot, generated filenames and a 5 MB limit. Positive avatar upload still needs a dedicated runtime check.
- Removed misleading Commercial fallback from property subtitles. The subtitle comes from saved portfolio zoning and property type; it is not inferred from the address.
- Visible currency audit found Rand/ZAR formatting; legacy internal companyDollar field names remain for compatibility.

## Reproducible local checks

Run scripts/qa/verify-agency-access.py, verify-edit-workflows.py and verify-subagency-creation.py against running local services. The latter creates/reuses clearly labelled QA records: QA Local Subagency Verification and qa-subagency-user@example.invalid, plus its broker profile and a R1,000 monthly target.

scripts/qa/start-local-services.py starts missing services from Debug builds and leaves occupied ports alone. Local QA email delivery is disabled for services it starts. Do not reuse recorded process IDs after a restart.

scripts/qa/verify-browser.cjs requires Puppeteer installed outside the repository (default /tmp/real-estate-browser-qa/node_modules/puppeteer) and Firefox. Temporary dependencies may need reinstalling after a restart.

## Outstanding: real PayFast sandbox acceptance

User supplied https://api.biscovetech.com as staging. The local notification configuration now points to /gateway/payments/webhook/payfast on that domain. PayFast remains the default provider.

Staging /health, /gateway/payments/subscription/status, /gateway/payments/webhook/payfast and /api/payments/subscription/status returned 404. The direct Cloud Run gateway also returned 404 for subscription status, indicating the deployed gateway needs updating before a real round trip.

No cloud deployment, provider payment or successful external webhook delivery has been completed. Next: inspect the deployed gateway routing and downstream configuration, prepare and validate the targeted deployment, then complete sandbox checkout and verify the signed webhook and subscription transition. Do not describe simulated webhook tests as provider end-to-end acceptance. Do not create a tunnel; user selected staging.

## Deferred

Google Places area autocomplete/schema/proximity work requires the requested approach review before code changes. Broad Google Cloud infrastructure deployment remains separate from the targeted staging payment fix.

No remaining-credit meter is available to the coding agent. Preserve compiling checkpoints and update this file before stopping.

## Update 2026-09-16: deployment preparation

Tenant/user/contact consistency is explicitly parked by the user.

- Shared GoogleAddressPicker added to property search editing, property detail editing and map-workspace creation. It fills formatted address and coordinates. Manual address edits clear stale coordinates. Removed the old Nominatim search and fabricated Cape Town coordinate defaults.
- Uses existing VITE_GOOGLE_MAPS_API_KEY; enable Maps JavaScript API and Places API (New) alongside Maps Static API. Live Google selection still requires verification with the configured key. No database migration is needed for existing address/coordinate columns; no Google place ID is persisted.
- Shared NameAvatar added to the sidebar user and tenant selector, with failed-image/initials fallback. Profile uploads update the session avatar. Other user/agency tables have not all been converted.
- Currency scan of both frontend source trees found no USD/dollar labels. Property subtitle uses saved portfolioZoning and propertyTypeDetails.name. Demo zoning is seeded using record-index rules, not geocoded from addresses.
- Frontend production build and gateway build pass. Existing browser smoke checks pass (directory, deals, sidebar, profile, responsive billing).
- Gateway now supports DOWNSTREAM_SCHEME for HTTPS downstream Cloud Run services (default remains http locally).
- Read-only cloud checks identify gateway-00006-gtq as the current ready gateway revision in project project-ff46407b-1a09-4376-95a, us-central1. No _HOST/_PORT/_SCHEME environment overrides were found. Direct Identity /api/payments/subscription/status also returned 404; gateway-only deployment will not complete payments.
- Automatic approval review rejected the proposed gateway source upload to Cloud Build because the exact payload/destination had not been explicitly confirmed. A user approval question is pending for the isolated gateway source bundle and realestate-images registry in that project. Do not bypass that rejection. No image was built in Cloud Build and no service was deployed.
- Proposed gateway tag: qa-20260916; intended source bundle: Program.cs, project file, Dockerfile, ocelot.json, appsettings.json (gateway configuration only; no .env files). Recreate the temporary build bundle after approval; the rejected tool call did not run.

## Pre-deployment navigation corrections

- Agency Overview (formerly Agency at a Glance) is under Administration. AgencyAdmin and PlatformAdmin access is role-based in the menu, page and overview API; the API retains tenant filtering and excludes brokers.
- Reports & Analytics was extracted into ReportsAnalytics.jsx and moved under Reports. Its report cards and charts are no longer part of System Admin. Three remaining dollar prefixes were corrected to Rand.
- Amenity Types is hidden and not rendered in System Admin for AgencyAdmin; PlatformAdmin retains access.
- Tenant-switcher roster ignores the active filter. A PlatformAdmin-only Identity tenant-roster endpoint includes tenants without CRM agency rows. Platform default reads and explicit All Tenants reads do not silently narrow to the login tenant. Fixed active-option highlighting (tenant ID versus agency ID). Subtree queries now load the selected parent rather than all parents on startup.
- Frontend build and 36 backend tests pass. Live APIs: AgencyAdmin overview 200; AgencyAdmin full-roster 403; PlatformAdmin full-roster 200 with 2,005 local tenants. Expanded browser checks pass for AgencyAdmin navigation, analytics, directory, deals, profile and billing.
- Initial production administrator email confirmed as esauselo@gmail.com, with no preset password. Single-use email setup/bootstrap is still pending implementation; an empty password must not permit login.
- Deployment remains paused. No cloud databases or services changed.

## Resumed metrics and lead-stage verification

Lead forms and charts now use Contacted, AttemptedContact, Nurturing, Disqualified, Qualified in enum order. Zero-count stages remain visible; deal stages are excluded. Numeric values map correctly and backend updates reject undefined enum numbers. scripts/qa/verify-lead-stages.mjs passes; frontend build passed; resumed backend suite: 36 passed.

Broker metrics Unavailable was caused by the local CRM process using the Docker gateway hostname. Local starter now sets Gateway__BaseUrl=http://localhost:5000/gateway; restarted CRM and verified broker metrics HTTP 200, activeListings=1, averageDom=1019 for the seeded broker. DOM uses property CreatedAt, not a dedicated listed-on date. Company Share remains null because Brokerage__CompanySharePercent is absent; the business percentage has been requested, not assumed. Cloud services still require their own gateway URL. Added logging for non-success property metric responses and reused the HTTP client factory.

No deployment or database deletion performed.

Company-share decision confirmed: ignore agency retention percentages for now and attribute 100% of broker deal commission to the company. Broker metrics now use 100% consistently for totals, month filters and monthly trends; Brokerage__CompanySharePercent is no longer needed for this reporting flow. This supersedes the pending percentage question above.
