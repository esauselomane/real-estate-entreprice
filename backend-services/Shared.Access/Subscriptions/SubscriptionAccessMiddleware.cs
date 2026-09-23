using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Access.Subscriptions;

/// <summary>Checks persisted billing entitlement for authenticated business API requests, including direct service calls.</summary>
public sealed class SubscriptionAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IHttpClientFactory clients, IConfiguration configuration, ILogger<SubscriptionAccessMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null
            || context.GetEndpoint() == null ||
            (context.GetEndpoint()?.Metadata.GetMetadata<Shared.Access.SampleData.SampleDataEndpointAttribute>() != null && context.User.IsInRole(Shared.Access.SampleData.SampleDataBlueprint.ServiceRole)) || context.User.IsInRole("SuperAdmin") || context.User.IsInRole("PlatformAdmin"))
        {
            await next(context);
            return;
        }
        var identityUrl = configuration["Identity:BaseUrl"];
        if (!Uri.TryCreate(identityUrl, UriKind.Absolute, out var identityUri) || identityUri.Scheme is not ("http" or "https"))
        {
            await UnavailableAsync(context);
            return;
        }
        try
        {
            using var client = clients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{identityUrl!.TrimEnd('/')}/api/payments/subscription/status");
            request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
            using var response = await client.SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                await UnavailableAsync(context);
                return;
            }
            var status = await response.Content.ReadFromJsonAsync<SubscriptionAccessResponse>(cancellationToken: context.RequestAborted);
            if (status?.RequiresUpgrade == null)
            {
                await UnavailableAsync(context);
                return;
            }
            if (context.User.IsInRole("AgencyAdmin") || context.User.IsInRole("Admin") || context.User.IsInRole("Manager"))
            {
                using var scopeRequest = new HttpRequestMessage(HttpMethod.Get, $"{identityUrl!.TrimEnd('/')}/api/auth/visible-tenants");
                scopeRequest.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
                using var scopeResponse = await client.SendAsync(scopeRequest, context.RequestAborted);
                if (!scopeResponse.IsSuccessStatusCode)
                {
                    await UnavailableAsync(context);
                    return;
                }
                context.Items["VisibleTenantIds"] = await scopeResponse.Content.ReadFromJsonAsync<List<string>>(cancellationToken: context.RequestAborted) ?? [];
            }
            if (status.RequiresUpgrade.Value)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                await context.Response.WriteAsJsonAsync(new { code = "subscription_required", message = "Your agency subscription requires an upgrade. Open billing to restore access." });
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (context.RequestAborted.IsCancellationRequested) return;
            logger.LogWarning(exception, "Subscription entitlement could not be verified");
            await UnavailableAsync(context);
            return;
        }
        await next(context);
    }

    private static Task UnavailableAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return context.Response.WriteAsJsonAsync(new { code = "subscription_check_unavailable", message = "Subscription verification is temporarily unavailable. Please retry." });
    }
}
