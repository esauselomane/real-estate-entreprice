using Developer.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Developer.API.Middleware
{
    public class ApiKeyAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiKeyAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, DeveloperDbContext db)
        {
            var path = context.Request.Path;
            var requiresApiKey = path.StartsWithSegments("/api/developer/properties") ||
                path.StartsWithSegments("/api/developer/owners") || path.StartsWithSegments("/api/developer/submissions");
            if (!requiresApiKey) { await _next(context); return; }
            var apiKey = context.Request.Headers["X-Api-Key"].ToString();
            context.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "A valid agency API key is required." });
                return;
            }

            var keyHash = ApiKeyGenerator.HashKey(apiKey);
            var apiKeyRecord = await db.AgencyApiKeys
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked, context.RequestAborted);

            if (apiKeyRecord == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid or revoked API key." });
                return;
            }

            var expectedAgencyId = context.Request.Headers["X-Expected-Agency-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(expectedAgencyId) && expectedAgencyId != apiKeyRecord.AgencyId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "The configured agency does not match the API key." });
                return;
            }

            if (apiKeyRecord.ExpiresAt.HasValue && apiKeyRecord.ExpiresAt.Value <= DateTime.UtcNow)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "API key has expired." });
                return;
            }

            if (!string.IsNullOrWhiteSpace(apiKeyRecord.AllowedIps))
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();
                var allowedIps = apiKeyRecord.AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!allowedIps.Contains(remoteIp ?? ""))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "IP address not allowed for this API key." });
                    return;
                }
            }

            apiKeyRecord.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(context.RequestAborted);

            context.Items["AgencyId"] = apiKeyRecord.AgencyId;
            context.Items["ApiKeyName"] = apiKeyRecord.Name;
            context.Items["ApiKeyId"] = apiKeyRecord.Id;

            await _next(context);
        }
    }
}
