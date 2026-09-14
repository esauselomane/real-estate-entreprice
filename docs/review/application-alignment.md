# Application alignment audit and change map

## Confirmed findings

- BrochureBuilderWorkspace uses the signed-in user's profile, not deal assignments. CRM's generation endpoint trusts broker and agency branding supplied by the client and chooses the first visible tenant instead of resolving the deal's tenant.
- The PDF cover supports only one broker. A collection must travel from server-resolved deal assignments through the queued job into the cover renderer.
- Subscription package editing has no dedicated admin interface. SuperAdmin is normalized to PlatformAdmin in the frontend; an exact original-role check is needed for the requested restriction.
- Agency Glance treats trial subscriptions as current paid plans and disables the package button. Billing selection must remain available during trials.
- Agency summary labels broker count as total users, and the growth series counts new broker records per month. Labels currently imply all users or cumulative growth, which is misleading.
- CRM report endpoints mix estimated and realised commission, while broker groupings use the entire comma-separated allocation string. Reconciliation must distinguish agency revenue from individual allocated commission.
- Registration and brochure email templates are loaded from brittle filesystem paths. Template values are inserted without HTML encoding. The PDF renderer has separate embedded markup, so changing HTML template files alone would not update the PDF.

## Change map

1. Server-resolved deal presentation DTO and broker cover helper; update preview and PDF together.
2. Exact-SuperAdmin package API and feature-local admin screen; retain immutable package codes and deactivate rather than delete.
3. Repair trial upgrade entry, preserve agency selection and validate billing permissions.
4. Shared email rendering/branding library for Identity and CRM, with system-logo asset and agency branding for business communications.
5. Central commission definitions and comparable report populations; add reconciliation tests and document what cannot be verified without production data.
6. Group new/changed code by feature. Avoid unrelated mass file moves; keep existing endpoints and imports compatible.

## Validation approach

Build both backend services and frontend. Add targeted tests for broker selection/rendering, role enforcement, pricing validation, trial upgrade, HTML escaping/branding and financial calculations. Use isolated data and email previews; do not send real emails as a test. Existing payment tests form the baseline (11 passing before this task).

## Implemented

- Brochure preview and generated PDF resolve agency and ordered broker contacts from the actual deal. Multiple contacts render side by side; client-supplied branding/contact data no longer determines the PDF cover.
- SuperAdmin-only package CRUD (create, list, update, deactivate) is available under System Admin. API role enforcement is independent of frontend role normalization. Package codes remain immutable.
- Trial users can explicitly choose an upgrade in Agency at a Glance. Expired trial users can authenticate to reach billing; the frontend subscription gate restricts their workspace. PayFast remains the default.
- Shared.Presentation owns the email layout, encoding, safe links and embedded system logo. Registration/password-change/billing emails use system branding. Brochure and targeted reminder emails use agency branding. Reminder recipients resolve within the caller's visible agencies.
- CRM reporting helpers centralize commission, month buckets and leaderboard rows. Revenue uses ClosedWon and the close date; explicit zero commission remains zero. Pipeline commission excludes lost deals. Broker/team revenue is labeled as deal revenue, not a payout calculation.
- Deals list pipeline value now uses the server aggregate instead of the first page. Fixed duplicate frontend leaderboard service methods that silently overwrote one another and supplied incompatible fields to charts.
- Broker stage charts use actual stage counts instead of mixing compliance alerts with monthly closed counts. Missing listing data displays as unavailable. Agency company share is no longer hardcoded to 70%; configure Brokerage:CompanySharePercent if appropriate.
- Property aggregate endpoints now apply the list visibility filter; property growth starts at a calendar-month boundary. Broker target selection uses target year/month instead of the date the target was entered.

## Metric definitions and limits

- Agency totals cover visible agencies, all time. Annual reports count records by creation date and realised commission by closing date; these populations can legitimately differ.
- A broker's assigned-deal GCI includes the whole realised commission on each assigned deal. A co-brokered deal appears for each participant; broker performance rows must not be summed as agency revenue or interpreted as commission payouts. Team leaderboard rows count a deal once.
- Active listing and days-on-market figures are agency figures: the property model does not have a broker assignment relationship. The UI labels this scope and individual leaderboard listing counts show unavailable.
- Commission fallback for legacy ClosedWon rows with null realised commission uses the expected amount; explicit zero does not trigger fallback.

## Remaining verification / existing limitations

- Registration email and two-broker HTML samples were visually checked in headless Firefox. This is not an Outlook/Gmail delivery test or a full generated-PDF check. Production-data reconciliation is still pending; localhost:5001 was not reachable during this pass.
- Password recovery now persists hashed, expiring tokens and consumes them atomically with the password change. Recovery does not activate disabled accounts. New passwords are hashed; existing plaintext accounts retain login compatibility. The bootstrap preserves user-changed passwords. Configure Urls:FrontendClient and SMTP for real delivery. Existing issued JWTs are not revoked by a password reset.
- Shared.Access checks current Identity entitlement for authenticated CRM and Property business requests, including direct service calls. Expired access returns 402; unavailable verification returns 503. Platform administrators bypass this billing check. Identity login/billing routes remain reachable. This checks the caller's own tenant; subsidiary-tenant entitlement and broader authorization-boundary review remain separate concerns.
- Property visibility currently trusts AgencyAdmin tenant overrides in its existing list policy. Applying that policy aligns statistics with lists but does not validate agency hierarchy at the property-service boundary.
- No real SMTP messages, live PayFast charges, production mutations or deployments were performed. Sandbox notification URL/passphrase setup and real provider callback verification remain as described in docs/payments/sandbox-verification.md.
- Legacy templates under templates/ are not the source for these runtime email layouts. The shared renderer is authoritative; PDF content remains generated by the brochure worker.
- Corrected report chart field casing in RoleDashboard, SystemAdminWorkspace and ReportsWorkspace: the API emits camelCase, while chart bindings previously used PascalCase and displayed missing/zero values.

## Validation results

- 27 isolated tests pass, including payment orchestration, commission/date reconciliation, broker matching, shared email branding/encoding and package authorization metadata/validation.
- Identity and CRM compile through the test project; Property build passes; admin frontend production build passes.
- Existing backend compiler/package warnings and the frontend large-bundle warning remain. No production/browser end-to-end verification is claimed.


## Further corrections and review artifacts

- Property growth now maps numeric year/month API buckets into the monthly chart contract. Requests preserve the gateway path; failed property counts show unavailable instead of zero.
- Subscription-required API responses open the frontend billing flow immediately.
- The email configuration namespace matches its Configuration folder; the user-provided sender name is preserved.
- Reproducible sample generator: `dotnet run --project tools/PresentationPreview -- docs/review/previews`.
- HTML previews: [registration](previews/registration.html), [password change](previews/password-change.html), [agency reminder](previews/agency-reminder.html), [brochure email](previews/brochure.html), [two brokers](previews/brochure-brokers.html). All use fictitious sample data. Browser previews inline the logo; real system emails attach it by CID.
- No live service, SMTP delivery, provider callback, production migration, or deployment was performed in this verification pass.

## Cumulative property history

Agency at a Glance now shows cumulative property totals at each calendar month end, with the current month counted to date. Each total includes visible property records created before the chart window. Tenant and role filters remain applied. Counts describe records still present in the database; deleted properties and historical agency transfers cannot be reconstructed without an audit history. Monthly totals are snapshots and should not be summed together.

## Broker suggestions and maps

- Lead creation and editing share the same suggestion component, shown only while no broker is selected. It ranks active brokers by freshly computed overall score and requires an explicit selection. Requests are tenant-scoped and stale responses are discarded.
- Fixed non-translatable assignment filters, actual converted-lead percentage, no-history scoring, realised commission fallback, deterministic tie ordering and weight normalization. Removed misleading property-type scoring derived from property addresses and contextual score multipliers unsupported by reliable data. Scores are comparative performance metrics, not closing probabilities.
- Property Details uses Google Static Maps. Brochure maps now use an embedded Google static image with a single centred dotted enclosure. Configuration: docs/maps/google-maps.md.
- 29 tests pass, including broker suggestions against SQLite and static map URL/path construction. Live Google map appearance remains unverified until the key is configured.
