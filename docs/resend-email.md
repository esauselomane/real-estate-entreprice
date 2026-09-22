# Resend email configuration

Identity account invitations, password recovery and billing, and CRM reminders and
brochures use the official Resend .NET SDK. Both services require a key at startup.

## Local development

Configure .NET user secrets from the repository root:

```sh
dotnet user-secrets set "Resend:ApiToken" "YOUR_RESEND_API_KEY" --project backend-services/Identity.Microservice
dotnet user-secrets set "Resend:ApiToken" "YOUR_RESEND_API_KEY" --project backend-services/CRM.Microservice
```

User secrets load automatically in Development and are stored outside the repository.
Restart the services after setting the key. Alternatively, supply `Resend__ApiToken`
in each service's environment. Do not commit the key to appsettings.

Verify the sender domain in Resend. `Email:FromAddress` defaults to
`admin@biscovetech.com`. `Email:SupportEmail` sets reply-to. Identity's
`Urls:FrontendClient` and CRM's `Frontend:BaseUrl` must point to the public frontend.
Docker Compose production reads `Resend__ApiToken` from its environment or an
untracked .env file; it does not load development user secrets.

## Google Cloud production

Create a Secret Manager secret named `resend-api-token` in the production project,
with the Resend API key as an enabled version. The updated deployment scripts grant access
on that secret to the Identity and CRM runtime service accounts and configure:

`Resend__ApiToken=resend-api-token:${RESEND_SECRET_VERSION}`

`RESEND_SECRET_VERSION` defaults to `3`. Cloud Run fetches this secret at instance
startup and injects it into the environment. ASP.NET Core maps it to
`Resend:ApiToken`. No application-level Google credentials are needed.

For rotation, add a secret version, set `RESEND_SECRET_VERSION` to its number, and
redeploy both services. The generic cloud-run-manifest.yaml references version 3;
when using that manifest directly, update its version and grant Secret Manager
Secret Accessor on this secret to its `identity-sa` and `crm-sa` service accounts.
The shell scripts use `identity-service-sa` and `crm-service-sa` instead.

This migration does not itself create cloud secrets or deploy services.

## Verification and behavior

Run `dotnet test tests/Identity.Payments.Tests/Identity.Payments.Tests.csproj -m:1`.
The SDK tests use a fake HTTP handler; automated tests do not send real email.
Registration and onboarding tests use SQLite and verify rollback on email failure.
Password recovery tests verify token expiry and single use.

New user registration creates a pending account and sends a 48-hour password-setup
invitation. A supplied initial password is not used. Accepting a user invitation
does not reactivate a suspended agency. Provider submission and database commit
are not atomic: a commit failure after submission can leave an unusable email link.

A Resend message ID confirms API acceptance, not inbox delivery. Live tests require
a configured key and verified sender domain; check delivery events in Resend and
confirm receipt with the recipient.

References: [Resend SDK](https://github.com/resend/resend-dotnet),
[Cloud Run secrets](https://docs.cloud.google.com/run/docs/configuring/services/secrets).

## Payment approval confirmation

After a validated successful payment webhook updates the agency subscription to
Active, the same database transaction queues `payment-approved` for the agency's
contact email. This also covers renewals and recovery from failed payments. Retried
webhooks with the same provider event ID do not queue a second confirmation.
Incorrect amounts fail validation and do not queue approval emails.

The billing worker checks the queue every minute and sends through Resend, with
up to five attempts and exponential backoff. It no longer checks SMTP settings.
As with the existing worker, a crash after provider acceptance but before database
commit can cause a repeated email on retry; webhook deduplication is not a guarantee
of exactly-once email delivery.

Automated verification uses simulated PayFast COMPLETE events. To verify the full
sandbox path after deployment, complete a sandbox checkout for a test agency whose
contact email you control. PayFast's configured notify URL must be the public HTTPS
application webhook endpoint. Confirm the callback passes signature/provider and
amount validation, the subscription becomes Active, and the queued email is sent.
A browser redirect to a success page alone never triggers account activation or the
payment-approved email. No real payment is made by the simulated tests.
