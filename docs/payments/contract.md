# Subscription contract

The browser consumes camelCase payment DTOs through `/gateway/payments`.
The backend owns tenant authorization, pricing, provider selection and entitlement.
Provider SDK payloads never form the public API contract.

- `IPaymentGateway` is the only provider interface; factory registrations use typed HTTP clients.
- A configured default is used only for new subscriptions. Persisted provider ownership wins for existing subscriptions. Unknown legacy provider ownership requires reconciliation, never ID-prefix guessing.
- Trials remain free until an explicit checkout. Expired trials lead to the billing screen; starting checkout and browser return URLs never grant access.
- Existing trials receive a once-only two-calendar-month extension. Paid tenants are excluded. Startup must not overwrite subscription state.
- Enterprise is assisted by sales and cannot enter self-service checkout.
- Checkout uses database prices, monthly or annual billing, ZAR and card payments initially.
- Checkout response supports either GET redirect or POST form fields under the same contract.
- Webhooks must verify signatures and provider-specific validation before any state changes. Merchant reference and amount must match a persisted payment intent.
- Webhook deduplication, subscription changes and payment completion commit together. Failed events remain reviewable; retries must not duplicate entitlements.
- Cancellation retains access through the paid period. Past due/paused/expired access is blocked with billing recovery available.
- Provider configuration and secrets come from deployment configuration. Sandbox is the initial environment.

Peach product/account compatibility must be established against its actual API; the legacy Stripe-shaped implementation is not verified Peach support. Stripe is not an active provider.
