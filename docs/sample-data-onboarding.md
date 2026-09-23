# Trial sample data

The marketing trial onboarding page offers **Request sample data**, unchecked by default. Opting in records a `Requested` job in the same transaction as the new agency and its administrator. It does not create demo records or send invitations to demo brokers.

A PlatformAdmin opens **Agency at a Glance → Trial sample data requests**, chooses the requesting agency, confirms its address on the map (or enters verified coordinates), and clicks **Provision sample data**. The list comes from Identity, so newly onboarded agencies are visible before their first CRM record exists. Agencies that did not opt in cannot be provisioned through this endpoint. Agency administrators cannot list or approve requests.

## Scenario

Each agency receives five contacts, five brokers, five properties near the confirmed agency address, one company, one legal entity, five qualified leads and five deals across successive pipeline stages. Each property receives a pseudorandom Commercial, Residential or Land type and 2–5 units (suites, homes or plots). Choices are stable per agency/property so retries keep CRM interests aligned. Unit sale prices sum to the property price. Existing completed sample workspaces are not altered. Contact i owns property i; contact i+1 buys it. Each lead and deal share their buyer and broker, and deals point to the matching property, seller, buyer and source lead. Contacts link back to their deals and company/legal entity. Broker commission splits total 100%; only the closed-won deal has realised commission.

All names/addresses are labelled samples, coordinates are approximate, and emails use `example.invalid`. Broker accounts have no password or activation token and cannot sign in. Provisioning does not publish listings to syndication providers.

## Execution and retries

Identity polls approved jobs every 10 seconds. A conditional update grants a 10-minute lease; each attempt has a four-minute deadline. Three failed attempts require a PlatformAdmin retry. Only Pending/Processing jobs with an approver are eligible. Requested jobs stay idle.

The immutable plan and stable broker/contact IDs are persisted before queuing. Property and CRM each commit records and a per-tenant receipt in one transaction, guarded by a SQL application lock. Retrying returns the original receipt without recreating edited/deleted sample records. Cross-service operations are eventually consistent: partial records may be visible while provisioning is in progress; the completion email is sent only after both services finish. Retrying a failed email does not repeat data provisioning.

The email is delivered through the existing Resend service to the agency contact email captured at approval. Subject: **Your agency’s sample workspace is ready**. Completed means the email provider accepted the message, not proof of inbox delivery. Delivery is at least once: a process crash after provider acceptance but before recording NotifiedAt can cause a duplicate notification on recovery.

## Configuration

Identity:

- `SampleData__PropertyServiceUrl`: Property service base URL; Development defaults to `http://localhost:5002`.
- `SampleData__CrmServiceUrl`: CRM service base URL; Development defaults to `http://localhost:5003`.
- `Urls__FrontendClient`: real application URL used in the completion email.
- Existing `Resend__ApiToken`, email sender settings and shared `Jwt__Secret`/issuer/audience must be configured consistently.

The GCP deployment scripts set service URLs and the app URL. The Identity service needs continuously allocated CPU for the background worker (the scripts already use minimum instances and `--no-cpu-throttling`). For other deployments, apply equivalent worker hosting settings. Keep API credentials in the existing secret configuration.

Frontend uses the existing Google Maps picker/key; manual coordinates are available when Maps is unavailable. No extra geocoding API secret is needed. The administrator must confirm coordinates correspond to the agency address.

Service startup installs the new job/receipt schema using the repository’s existing schema-upgrade approach; CRM also includes a migration for the source-lead link and receipt table. Internal `/api/internal/sample-data` endpoints require a short-lived `SampleDataProvisioner` token matching the plan tenant. The normal gateway does not expose these paths.

## Verification

SampleDataTests check relationship integrity, exact record counts, repeat submissions, tenant isolation and admin-only approval metadata. SampleDataDeliveryTests check that unapproved requests remain idle, disabled demo accounts stay disabled, and email failures retry without calling the seeders again. Tests use temporary SQLite databases and fake HTTP/email delivery, with no real records or messages.
