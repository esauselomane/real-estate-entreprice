# Listing syndication

## Current implementation and boundary

Agency at a Glance now contains **Listing platforms**. Only PlatformAdmin users with agency-edit permission
use Add/Edit connection to save an agency API key, branch reference and any provider-defined
non-secret settings separately for Sandbox and Production. Agency administrators can view their connection status but cannot add, replace or remove
credentials, including through direct API calls. Only the saved-key status is
returned to browsers. Blank keys keep the existing value; removal is explicit.

Broker add/edit in the user workspace contains per-provider, per-environment AgentCodes.
Codes are stored on the CRM broker record; changing one provider does not remove others.
The CRM migration `AddBrokerSyndicationCodes` adds the storage column.

Property details contains **Listing portals**, without credential editing. Only providers with a saved agency API key or branch ID in the selected environment
appear in the compact platform list. Clicking a platform opens a wide mapping modal;
publishing remains disabled without a key or a ready adapter. The mapping preview shows property
values, branch mapping, photos, and temporary AgentCode/location-reference inputs. These
preview selections are deliberately not persisted or transmitted. Final broker assignment,
provider taxonomy, location lookup and rental mapping await the provider contract.
Publishing requires a saved key, provider readiness, property-edit permission and property
visibility. Removing a key also prevents queued Publish commands from transmitting.

The former `/properties/syndicate` endpoint returns a conflict instead of fabricating
an external listing ID or reporting a simulated publication. Existing legacy
syndication records are preserved but are **not** imported as confirmed live listings.
They require reconciliation with each portal before migration.

**Private Property is registered but not operational.** Its descriptor reports
`Ready=false`, and publishing cannot be enabled or queued. The user does not yet have
the authentication guide or sandbox branch/agent IDs. No outbound Private Property
calls are made and no signing algorithm or publication response semantics are guessed.
The sandbox WSDL establishes operations and structures, but not the complete
integration requirements: https://services.sandbox.pp.co.za/AgentImport/AgentImport.asmx?WSDL

Before enabling this adapter we still need:

- Provider-issued sandbox access, branch/agent IDs, and production onboarding requirements.
- SecurityToken digest algorithm, expiry rules and clock-skew requirements.
- Required listing/category/mandate/location fields and sale/rental price mapping.
- Confirmed semantics for UpdateListing responses, moderation, status and withdrawals.
- Media access/expiry constraints, rate limits and duplicate-submission handling.

No generated SOAP client, live listing mapping, location lookup, lead import or
analytics implementation is claimed by this foundation. Agent/branch values are
stored as mappings only; actual provider synchronisation remains part of that adapter.

## Adding a provider without changing the workflow

1. Create a trusted .NET assembly implementing `IListingProvider` (reference the
   current Property project for its contracts; these can be extracted to a contracts
   package when versioned provider distribution is needed).
2. Return a unique stable key and capability descriptor. Optional `Settings` fields
   describe non-secret connection values; the UI renders these without provider branches.
3. Implement validation, connection testing and Publish/Withdraw/CheckStatus. Own all
   provider-specific XML/JSON, authentication, mapping and error translation there.
4. Publish the adapter and its .deps.json/dependencies into
   `providers/<assembly-name>/` under the application directory, and list its assembly name
   under `Syndication:ProviderAssemblies` (or `Syndication__ProviderAssemblies__0`).
   The host discovers implementations on startup; duplicate provider keys fail startup.
   Restart/redeploy is required. This is trusted operator configuration, not DLL upload.
5. Add contract tests and sandbox verification, then set the descriptor's Ready flag.

The worker, coordinator, controllers and property panel do not dispatch on provider
names or numeric portal IDs. The test suite includes a second provider to verify this.
The current neutral snapshot contains address, description, sale/rental eligibility,
sale price, coordinates and photo URLs. Provider-specific enrichment and rental/unit
pricing need to be defined when implementing actual portal mappings; do not infer a
rental price from a property's sale price.

## Credentials

Inject `IProviderCredentialSource` into an adapter. Saved agency API keys are encrypted
using ASP.NET Data Protection, with separate purposes for tenant, provider and environment.
The resolver decrypts only on the server. Protected values are excluded from JSON responses.
Supplementary credentials can still come from configuration at
`Syndication:ConnectionCredentials:<connectionId>:<credentialName>`, supplied by Secret
Manager environment injection. A saved agency key takes precedence for `ApiKey`.

For production, set `Syndication__DataProtectionKeysPath` to a **persistent, access-controlled,
shared key-ring directory** mounted into all Property service instances. Back it up and
retain old keys across deployments. Container-local ephemeral storage is not sufficient.
Provision that mount before saving production credentials; saving a new key outside
Development is refused if this setting is absent. This change does not provision a Cloud
Run volume or deployment secrets. Apply the deployment's encryption-at-rest and access
controls to the key-ring volume. Development uses the normal ASP.NET local key ring.
Never put secrets into provider SettingsJson, listing snapshots, source control or logs.

## Durable processing

A request saves the publication's desired state and a `SyndicationCommand` in the same
transaction. Commands include a listing snapshot, revision and stable request key.
The worker polls every five seconds and ignores superseded unsent revisions. Live
status and URLs come only from provider results. Submitted responses schedule status
checks; they do not imply publication. Failed withdrawal retains the last confirmed
live state. Ambiguous outcomes require Check portal status before another publish.

SQL Server application locks serialize coordination and processing across instances.
The initial implementation uses one global syndication lock and one command per poll
with a 30-second operation deadline. This favours correctness at modest volume; move
to per-publication locks and provider rate-limit scheduling when throughput requires it.

Adapters MUST implement idempotent upserts/reconciliation using the canonical agency,
connection/environment and property identity, and honour the stable request key.
A crash after remote acceptance but before local commit can repeat a command. This is
at-least-once delivery, not an exactly-once claim. Adapters must not perform blind
non-idempotent creates. Only mark RetrySafe when retrying is known not to duplicate a
listing. Safe failures retry up to five attempts with exponential delay. Status checks
are bounded to twenty attempts. Unknown failures stop for operator review; credentials
and raw provider exception contents are not exposed.

Explicit Publish changes captures an updated snapshot; saving property details alone
does not currently trigger automatic publication. Durable commands must be resolved
before deleting their referenced property; foreign keys intentionally prevent losing
publication history by cascading a property deletion.

## Storage and verification

`SyndicationSchema` adds three SQL Server tables at Property service startup using the
repository's existing additive schema convention. It does not modify legacy tables.
Back up and review schema changes before deploying. SQL Server multi-instance lock
behaviour still requires environment verification; automated tests use SQLite.

```sh
dotnet test tests/Property.Syndication.Tests/Property.Syndication.Tests.csproj -m:1
cd frontend-client
npm run build
```

Tests cover persisted jobs across DbContexts, confirmed state, superseding queued
publishes, status polling, safe retry keys, uncertain outcomes, tenant isolation,
withdrawal failure and adding another adapter. They never contact a real portal.
