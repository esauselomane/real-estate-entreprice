# Biscove public property website

Separate, dependency-free Node.js website using the repository’s Developer API. Uses a professional dark palette: near-black (`#0d0b0e`), blackberry surfaces (`#241a25`), warm gold (`#d0ad68`) and ivory text (`#f3ede5`). Page structure follows the agreed property marketplace reference: Buy/Rent search, categories, hot properties, listing cards, details and owner submissions.

## Run locally

Requires Node.js 22 or newer and the existing .NET 10 services.

1. Deploy/start the updated Property microservice first. Its idempotent startup upgrade adds public listing fields with visibility **off**, preserves existing records and adds missing property types, including Land.
2. Configure Developer.API with `ConnectionStrings__DeveloperConnection`, `ConnectionStrings__PropertyConnection`, and the same JWT issuer, audience and signing secret as Identity. The property connection points to the same property database used by Property.Microservice. Start it with `dotnet run --project Developer.API --launch-profile http` (port 5006). Its startup creates owner account/session tables in the Developer database. Do not point local development at a production database.
3. In the agency portal, open **Agency Overview → Developer API · Public website**, select the destination agency and generate a key. Agency administrators can manage keys for their own agency; platform administrators explicitly select the agency. Copy the key when shown; only its hash is stored.
4. From this directory, copy `.env.example` to `.env`. Set `AGENCY_ID` to the destination tenant/agency ID and `AGENCY_API_KEY` to that agency’s generated key and `AGENCY_DISPLAY_NAME` to its name. `DEVELOPER_API_URL` is the Developer API route root, such as `http://localhost:5006/api/developer/`. If using the gateway, use its `/gateway/developer/` root instead.
5. Run `npm run dev` and open `http://localhost:3010`. No dependency installation is needed. Both `.env` and optional `.env.local` overrides are supported. `PUBLIC_SITE_ORIGIN` must match the exact browser origin, including port. Set it to `http://127.0.0.1:3010` if using that hostname instead of localhost.

The destination is configured through `AGENCY_ID` and `AGENCY_API_KEY`. The Developer API checks that the configured ID matches the agency bound to the key; a mismatch returns 403 rather than routing to another agency. Visitors cannot choose or override the agency. Without a configured connection the website renders its layout and an explicit connection message; it does not display fabricated listings.

## Agency workflow

- Existing listings remain private until explicitly published.
- Open a property in the portal and use **Public website** to enter/review the owner’s public name, email and phone. These fields are a publication snapshot, separate from internal CRM records; private identity/compliance documents are never included in the public contract.
- Check **Visible on public website** and save to approve publication. Only an administrator belonging to the property’s agency can select **Hot property**. Withdrawn listings are excluded from public list, detail and hot endpoints. Owner or non-admin edits preserve the administrator’s hot designation while withdrawing publication.
- In Property Search, use **Owner submissions awaiting review** to find incoming properties. The count reflects the current property search filters. Open a submission to review its photos, units and owner contacts, then approve using the same publication settings.
- Public website owners are stored separately from staff/Identity users. Their properties include the owner account reference and public contact snapshot. They do not create agency staff accounts or obtain internal CRM access.

## Owner workflow

**Sell or rent through us** offers account registration/sign-in, property details, coordinates, photographs and units. Owner accounts are agency-specific. Owners explicitly consent to publication of their contact details. New submissions appear in the agency’s ordinary property inventory as **Pending review**, unpublished.

**My properties** shows only the signed-in owner’s submissions with review status and editing. Every owner edit unpublishes the listing and returns it to review while preserving the administrator-set hot designation. Existing unit IDs are preserved to retain operational links; owners can add units or mark them occupied, but cannot delete existing units. Property and unit photo uploads accept JPEG, PNG and WebP up to 2 MB each (10 photos across the property and units; 30 MB total JSON request). Owner sessions expire after eight hours and logout revokes the session.

Direct communication is supported through the owner’s phone/email. Online offers, contracts, signatures and payments are outside this implementation.

## Search and nearby areas

Search supports the shared property type catalogue, sale/rent, price, location, hot-only selection and pagination. Rental price bounds must match the same unit. All results are scoped to the key’s agency and approved public listings.

Nearby results use great-circle distance. The API accepts a latitude/longitude origin; for a text-only location it uses the first approved listing whose address contains that location as a deterministic area anchor. Exact area matches appear before nearby listings. The website explains that distances are from a listed property, rather than an administrative area centre. If no anchor is available, only text matches are shown with an explanatory message. This implementation does not depend on an external geocoder. Owner submissions require coordinates; the form explains how to obtain them from Google Maps.

## API contract

All public calls require `X-Api-Key`. Owner calls additionally require `X-Owner-Token` after registration/sign-in. The website server adds these headers; neither credential is exposed to browser JavaScript. Owner tokens are held in an HttpOnly, SameSite=Strict cookie, with Secure in production, and stored only as hashes in the Developer database.

| Method | Developer API path | Purpose |
| --- | --- | --- |
| GET | `/api/developer/properties` | Approved listings/search |
| GET | `/api/developer/properties/types` | Shared property type names |
| GET | `/api/developer/properties/hot` | Approved hot listings with full public detail |
| GET | `/api/developer/properties/{id}` | Approved listing, images, units and owner contacts |
| POST | `/api/developer/owners/register` | Register owner in the key’s agency |
| POST | `/api/developer/owners/login` | Create owner session |
| POST | `/api/developer/owners/logout` | Revoke owner session |
| GET | `/api/developer/submissions` | Current owner’s properties and review statuses |
| POST | `/api/developer/submissions` | Submit a property for review |
| PUT | `/api/developer/submissions/{id}` | Edit an owned property and resubmit |

Search query parameters: `location`, `propertyType`, `listingType=Sale|Rent|Both|All (`Room` is a legacy compatibility alias)`, `roomLettingType=All|PrivateRoom|SharedRoom`, `minPrice`, `maxPrice`, `includeNearby`, `radiusKm` (1–100, default 15), optional `latitude` and `longitude`, `hotOnly`, `page` and `pageSize` (1–48).

Agency JWT endpoints remain `/api/developer/keys`, `/keys/generate`, `/keys/{id}/revoke`, and the property service’s `PUT /api/properties/{id}/public-listing`. Existing ambiguous Developer property proxy routes were replaced with this public contract. Internal leases, maintenance, verification documents, mandates and operational financial data are excluded. Legacy CRM proxies require platform administrator JWT authentication.

## Architecture and hosting

The browser uses only the website’s allowlisted same-origin `/api/` proxy. State-changing requests require the configured Origin and JSON. The server applies a Content Security Policy and no-store responses. Remote HTTPS image URLs are displayed directly; previously downloaded images or independently hosted images cannot be recalled by unpublishing.

Developer.API uses the **shared PropertyDbContext model** against the existing Property database, rather than forwarding public calls using a privileged gateway token. Property.Microservice owns schema upgrades and agency publication approval; Developer.API writes only constrained owner submission fields. This is an explicit shared-database boundary using the Developer API’s existing property-database connection. Deploy both updated services together before enabling the website. No existing data is automatically published.

For hosting, use `npm start`, Node.js 22+, `NODE_ENV=production`, an HTTPS `PUBLIC_SITE_ORIGIN`, and secret environment values. A Dockerfile is included. The website and Developer API have not been added to the existing live deployment pipeline or deployed by this change. The current SQL Server startup upgrades require the relevant schema permissions. Data-path tests use isolated SQLite databases; live SQL Server rollout must run the idempotent upgrades in the target environment.

## Verification

- `npm run build` — validates the website/server JavaScript.
- `npm test` — isolated HTTP proxy, cookie, origin and route-boundary tests.
- `dotnet test ../tests/Developer.PublicListings.Tests/Developer.PublicListings.Tests.csproj` — agency/key isolation, public visibility, hot/nearby search and owner edit/review rules.
- Optional browser QA: set `QA_PUPPETEER_PATH` to an installed Puppeteer package and `QA_BROWSER_PATH` to an installed Chromium browser, then run `node tests/browser-smoke.cjs`. Uses isolated synthetic listing fixtures and writes screenshots to `artifacts/property-public-site/`; it never modifies the live database.

## Local sample data

See [SAMPLE_DATA.md](SAMPLE_DATA.md) for the SuperAdmin-linked Agency 1 fixtures, owner sign-in, local URLs and repeatable API verification. The local-only seed tool creates 50 labelled properties without duplicating or resetting existing fixture edits.

**Room share/let** is a property type with a PrivateRoom/SharedRoom subtype. Sale and letting are independent; only listings explicitly offered to let require a monthly unit price. For sale and To let may both be selected. The Both search mode requires both flags; All includes every approved offering.

The landing page requests `/properties/hot?pageSize=5&randomize=true`, selecting five distinct records from the full approved hot pool. API responses include `thumb`: an existing `_thumb` image where available, otherwise a 150×100 JPEG derived from the gallery. Full-resolution galleries remain available on detail pages. Public browsing/search/contact details do not require visitor authentication; the API key is held on the website server. Owner submission and editing still require an owner account.

Search results suggest individual nearby areas and property counts, based on matching published listings within the radius. Suggestions are returned even when nearby results are not included. Checkbox changes are staged until Apply filters is clicked; clearing all selections restores the original location search. Applying preserves the other filters and resets pagination. `selectedNearbyAreas` may be repeated in the query string. Public site location searches default to the requested area only.

### Worldwide structured locations

`GOOGLE_MAPS_BROWSER_KEY` enables worldwide Google Places address suggestions on the owner submission form. This is a public browser key: restrict its allowed website referrers and Maps/Places APIs in Google Cloud. Agency API keys remain server-only. Without Google, owners can enter and confirm the address components and coordinates manually.

The agency portal also captures country code/name, region (province/state), district/county, city and suburb. Confirm these fields before saving. No country requires every administrative level. A deterministic internal location ID includes country and the full administrative path; equal suburb names across provinces or countries are separate locations. The catalogue reuses confirmed locations rather than parsing comma positions. A property's Google place ID identifies its address, not its suburb.

Both searches suggest confirmed locations from inventory visible to that user/agency. Selecting a suggestion sends its `locationId`. Public searches return HTTP 409 for a bare suburb/city name matching multiple confirmed locations. Nearby checkboxes carry location IDs where available, retaining legacy names for older unstructured listings. Distances still use an actual listing as the area anchor rather than an official suburb boundary.

Existing addresses are **not automatically guessed or bulk-geocoded**. Reopen a property, select its Google result, confirm the location fields and save. Until confirmed, legacy free-text search remains available and can be ambiguous; exact location-ID searches intentionally exclude unresolved records. Do not derive suburb/country by splitting arbitrary addresses.

Only user-confirmed property address details belong in the business catalogue. Do not use the catalogue to harvest Google Places results. Google place IDs may be retained; other Google-sourced content is subject to the applicable Google storage and attribution terms. Preserve provenance when adding a bulk migration or another provider.

### Public amenities, parking and rental deposits

Property details expose active amenity records with active catalogue types, scoped to the listing's agency. Property features appear together; unit-specific features stay under that unit. Pets, swimming pool, parking, security and gym use matching icons. Parking shows the captured bay count; false boolean values are shown explicitly. Internal amenity notes are not published.

`AdvertisedDepositAmount` is an optional unit listing field, editable in the agency unit editor and owner submission form. It is independent of the private lease's `DepositAmount`. Null means “On request”; zero means “No deposit required”. Deposits are displayed for letting listings, without publishing tenant lease data.

For local fictional fixtures only, run the sample tool with `--confirm-sample-locations --seed-public-features` to populate known location metadata, configured amenity examples and advertised deposits. Existing real listings are excluded by owner ID, agency and the sample marker. Google address metadata for real listings still requires confirmation.

### Search engines and social previews

SEO metadata is generated server-side: page-specific title/description, canonical URL, robots directives, Open Graph, Twitter cards, author, and JSON-LD website/agency/page/listing/breadcrumb data. Property pages also contain a readable summary before JavaScript loads. Withdrawn/missing listings return 404; upstream/configuration failures return 503 with Retry-After and noindex. No fabricated reviews, ratings or dates are generated.

Set `PUBLIC_SITE_ORIGIN` to the real HTTPS website origin and `NODE_ENV=production` when launching. Local development remains noindex and disallowed in robots.txt. Owner/account pages and detailed filter combinations are noindex; public listing-purpose category pages are indexable. Tracking/referral parameters do not enter canonical URLs. Production `/robots.txt` advertises `/sitemap.xml`, which indexes `/sitemap-pages.xml` and paginated `/sitemap-properties-N.xml` files containing only the agency's approved public listings. An upstream failure returns an error rather than publishing a misleading empty production sitemap.

`SEO_SOCIAL_IMAGE` optionally supplies a public image URL (prefer a 1200×630 image); otherwise property HTTPS photos are used when available, falling back to the logo. Inline/base64 gallery data is not used in sharing tags. `SITE_CURRENCY` defaults to ZAR and must match listing prices. Optional `GOOGLE_SITE_VERIFICATION` and `BING_SITE_VERIFICATION` values come from your webmaster accounts; no account verification or sitemap submission is performed automatically.

### Agency branding, room categories and hot permissions

Set `PUBLIC_LOGO_URL` and `PUBLIC_FAVICON_URL` in `.env`, then restart the website. Values may be absolute HTTPS URLs or local public asset paths; defaults are `/logo.jpg` and `/favicon.svg`. Custom local files can be placed under `public/branding/` and referenced as `/branding/agency-logo.png` (PNG/JPEG/WebP/SVG/ICO supported). Never place API keys or private documents in this directory. Invalid schemes fall back to the bundled branding. Branding is included in the initial HTML and the logo is also the default social-preview fallback.

**Room share/let** is a property type. Private/shared room is a subtype; sale and letting are independent purposes. Both homepage card sections and the initial search default to rooms, across sale/letting purposes. The hot-room section samples up to five eligible rooms and does not fill empty spots with unrelated property types. Existing room offerings are migrated to the room property type without changing their prices, purpose flags, publication or hot designation. Historical `listingType=Room` website links redirect to the corresponding room-type/rental search; the Developer API retains that legacy alias for compatibility.

The portal’s hot checkbox is read-only unless the signed-in administrator belongs to the property’s own agency. The API enforces the same rule using authenticated claims; tenant switching, hierarchy access and platform-wide visibility do not grant another agency’s hot controls. Other permitted publication edits can omit the nullable hot field to preserve its value. Owner edits withdraw publication and retain the admin-set designation; withdrawn records never appear on public hot endpoints. Website owner accounts remain separate from CRM Owner Contacts.

### Owner withdrawal

A signed-in owner can select **Remove from website** on their own published listing in **My properties**. The server-scoped `POST /api/developer/submissions/{id}/withdraw` sets visibility false and review status Unpublished. The agency's property record, units, owner link and administrator-controlled hot designation are retained. Withdrawn properties disappear from public search, hot lists, detail pages and generated property sitemaps. The operation is idempotent and limited to the authenticated website owner and configured agency. No owner publish/republish operation exists; resubmission still requires agency review and publication.

### Letting expiry and photographs

Agency publication starts a two-calendar-month letting term. Normal edits, owner resubmission and repeated approval do not reset an existing term. Agency publication settings offer an explicit renewal checkbox. Public API queries immediately exclude expired rental-only listings, and a five-minute background job records their Expired status. For mixed sale/letting listings, only public rental availability/prices/deposits expire; the sale listing remains public. Operational rental flags and lease records are retained. Existing published rental listings receive a two-month term when the schema upgrade first runs.

The agency property search includes **Letting listings — expiry report**, with UTC date range selection and CSV export. Results follow the same authenticated tenant/visibility rules as property search.

Property and unit photos share a **10-picture limit per property**, with a maximum **2 MiB per uploaded photo**. Existing references can be retained, but new external image links are not accepted. New uploads in both the agency portal and owner website use the same validator/normaliser: supported JPEG/PNG/WebP content, bounded dimensions, a normalised JPEG up to 1200 pixels, and public thumbnails. Image bytes remain in the existing database gallery storage rather than relying on ephemeral production disks. The base64 JSON submission cap is 30 MB.

### Property assistant

Set the server-only `OPENAI_API_KEY` and optionally `OPENAI_CHAT_MODEL` (default `gpt-4.1-mini`, a configurable Responses API tool-calling model). Restart the website after changing environment variables. The assistant stays hidden when its OpenAI or agency connection is missing. No login is required to use it. The browser never receives either API key.

The assistant can search approved listings, read details and resolve locations/nearby areas through this site's configured agency Developer API. It asks before broadening to nearby areas. Search results link to the real listing and use its thumbnail. The chat does not provide owner account actions, publish properties or read private CRM information. Messages and tool results are sent to OpenAI with `store: false`; the site keeps the current conversation in page memory only. The panel discloses that OpenAI processes the conversation. Request size, conversation length, tool calls, timeouts and per-process request rates are bounded; production can additionally apply shared ingress limits across instances.

`prepare_enquiry` produces a signed 15-minute draft. It collects name, surname, email and cellphone (name OR cellphone is required), property purpose and enquiry text. The visitor reviews the draft and clicks **Confirm and send** before any lead is created. Cancelling or asking another question discards the review. Failed delivery can be retried with the same request ID; CRM stores one lead per agency/request ID, without automatically creating a CRM contact or login. The Developer API rechecks that the listing and its requested sale/rental purpose are still public before forwarding.

For enquiry delivery configure `CRM__BaseUrl` on Developer.API to the CRM service origin (e.g. `http://localhost:5003`). Developer.API and CRM must have the same securely configured `Jwt:Secret`; this signs a purpose-specific enquiry payload with a short timestamp, rather than granting anonymous users a staff token. Missing configuration fails closed. No database schema changes or sample data are needed.

Campaign links such as `/?ref=MARKETER_1` set a server-written HttpOnly `biscove_referral` cookie for 30 days (letters, numbers, `_`, `-`, maximum 80 characters). The latest valid referral is copied into the confirmed lead notes. It is attribution only: it does not redeem a discount, validate a registered marketer, or provide a conversion dashboard.

Production: bind the OpenAI key from Secret Manager to `OPENAI_API_KEY` on `property-public-site`; configure the CRM service URL on Developer.API; deploy CRM, Developer.API and the website together. Do not place keys in Cloud Build substitutions, client configuration or committed `.env` files. Official tool-calling reference: https://developers.openai.com/api/docs/guides/function-calling.
