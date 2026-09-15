namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Shared subscription email copy. No payment details or credentials are included.</summary>
public static class BillingEmailTemplates
{
    public static string Subject(string template) => template switch
    {
        "welcome" => "Your subscription is active",
        "trial-ending" => "Your free trial ends soon",
        "past-due" => "Your subscription payment needs attention",
        "payment-failed" => "Your subscription payment failed",
        "recovered" => "Your subscription payment recovered",
        "cancelled" => "Your subscription was cancelled",
        _ => "Subscription update"
    };

    public static string Body(string template) => template switch
    {
        "welcome" => "<p>Your paid subscription is active. Sign in to continue using your agency workspace.</p>",
        "trial-ending" => "<p>Your free trial ends within three days. Sign in and open billing to choose a paid plan. You will not be charged automatically.</p>",
        "past-due" or "payment-failed" => "<p>We could not confirm your subscription payment. Sign in and open Manage Billing to update your payment method, or contact billing support.</p>",
        "recovered" => "<p>Your payment has been confirmed and subscription access has been restored.</p>",
        "cancelled" => "<p>Your subscription was cancelled. Access remains available until the end of your paid period.</p>",
        _ => "<p>Your subscription status changed. Sign in to review billing.</p>"
    };
}
