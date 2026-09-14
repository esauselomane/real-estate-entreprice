# Payment verification and operations

## Configuration

PayFast is the initial provider. Peach is deliberately unavailable until its real recurring product is implemented and verified. Stripe is not registered.

Set deployment environment variables (never commit credentials):

- `Payments__DefaultGateway=PayFast`
- `Payments__EnabledGateways__0=PayFast`
- `Payments__AllowedReturnOrigins__0=http://localhost:5173` (or the exact deployed application origin)
- `PayFast__MerchantId` and `PayFast__MerchantKey`: your sandbox account values
- `PayFast__PassPhrase`: the passphrase set on that sandbox account (required for recurring billing)
- `PayFast__BaseUrl=https://sandbox.payfast.co.za`
- `PayFast__ProcessUrl=https://sandbox.payfast.co.za/eng/process`
- `PayFast__WebhookUrl=https://YOUR-PUBLIC-API/gateway/payments/webhook/payfast`
- Marketing: `NEXT_PUBLIC_GATEWAY_URL` is the gateway origin, `NEXT_PUBLIC_APP_URL` is the application origin.
- Application: `VITE_SALES_URL` is the marketing contact page.

Recommended next step: use a dedicated staging deployment with its own database and PayFast sandbox account. For local-only testing, the PayFast documentation suggests a temporary HTTPS tunnel (for example ngrok) to the gateway port; set the generated public origin plus `/gateway/payments/webhook/payfast` as the notification URL. Do not expose a production database through that tunnel.

PayFast must be able to POST to the public webhook URL. Do not point it at a PayFast host. HTTPS forwarding must preserve the form body. Do not rewrite signed fields. Secrets in existing appsettings are not proof of a working merchant account.

## Repeatable automated checks

Run `dotnet test tests/Identity.Payments.Tests` and `npm run build --prefix frontend-client`.
Run `npx --prefix client-facing-app tsc --noEmit --project client-facing-app/tsconfig.json`.

Database tests use isolated SQLite databases and a fake PayFast validation HTTP handler. They exercise transaction persistence, deduplication, wrong-amount rejection, tenant authorization, trial expiry, trial extension, checkout requests and payment recovery. They do not certify the SQL Server DDL upgrade or live provider acceptance.

## Sandbox acceptance

1. Back up the development database. Start Identity with the configured sandbox account. Startup adds billing columns/outbox and removes the single-subscription-per-tenant constraint. The existing trial extension is applied once; new trial registrations are marked as excluded.
2. Sign in as an agency administrator for a trial tenant. Open billing, select monthly or annual, and confirm the displayed price matches the backend catalogue. Enterprise must lead to sales.
3. Start checkout. Confirm a signed POST to PayFast contains the selected database amount, `subscription_type=1`, card-only payment and frequency 3 (monthly) or 6 (annual). Starting checkout must leave the trial unchanged.
4. Complete the sandbox card flow. Observe the HTTPS ITN request. Signature, merchant ID and provider postback must pass; amount and merchant reference must match the persisted intent.
5. Confirm a new provider-owned subscription, a succeeded payment intent and a processed webhook record. The browser must show active only after this update. Restart Identity and confirm the state survives.
6. Replay the same notification from PayFast's sandbox ITN dashboard. The paid period and notification count must not change.
7. Cancel checkout and confirm no new paid subscription. Use an expired-trial fixture and confirm the app opens billing rather than the workspace. Complete checkout and confirm workspace access returns after confirmation.
8. Simulate failed/recovered/cancelled events in the isolated test suite. Sandbox limitations mean these are not substitutes for production provider lifecycle certification.

PayFast's official SDK explicitly disallows customer card-update links in sandbox. Manage Billing is exposed only when the configured environment supports it. In production it opens the documented secure card-update link; subscription plan edits/cancellation remain assisted through merchant billing support.

## Failure review and retry

Platform administrators can GET `/gateway/payments/webhooks?page=1&pageSize=50` and POST `/gateway/payments/webhooks/{id}/retry`. Only stored verified payloads are replayed. Invalid signatures are rejected and logged, never stored as replayable events. Unmatched verified notifications remain recorded for review.

Do not replay an amount mismatch until the merchant agreement and local intent are reconciled. No endpoint changes the expected amount to make an unverified payment pass. A previously processed event is a no-op.

## Manual correction and rollback

Platform administrators can POST `/gateway/payments/subscription/override` with `tenantId`, `subscriptionId`, numeric `status`, `currentPeriodEnd`, and a meaningful `reason`. Optional `gateway` and `gatewaySubscriptionId` reconcile a legacy subscription after checking the merchant dashboard. Never infer the provider from an ID prefix.

Status values: trialing 0, active 1, past due 2, cancelled 3, expired 4, paused 5. Provider values: Peach 1, PayFast 2.

Each correction records the actor, reason and previous state in a `support.override` audit event. Roll back by issuing another correction with the recorded previous values and a reason referencing the earlier event. This changes local access only; it does not charge, refund or cancel a provider agreement. Provider financial actions must be reconciled separately.

The trial extension cannot repair dates overwritten by the old startup routine. Recover original dates from a backup or use an audited correction after confirming the intended entitlement.

## Email delivery

`BillingNotifications` is a durable outbox. Welcome, payment failure/recovery, past-due, cancellation and trial-ending messages use shared templates. Configure the existing `Email:Host`, `Port`, `FromAddress`, `Username`, `Password`, and `EnableSsl` settings. Without SMTP configuration messages stay pending. Failed deliveries retry with exponential backoff up to five attempts and retain their error for review. SMTP delivery is at-least-once: a crash after delivery but before commit may duplicate an email.

## References

- https://developers.payfast.co.za/docs/itn-instant-transaction-notification/
- https://github.com/PayFast/payfast-php-sdk/blob/master/lib/PaymentIntegrations/CustomIntegration.php
- https://developer.peachpayments.com/docs/v2-checkout-hosted
