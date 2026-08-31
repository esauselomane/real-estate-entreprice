using System.Text.Json;
using Developer.API.Data;
using Developer.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
            {
                await _next(context);
                return;
            }

            var apiKey = apiKeyValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                await _next(context);
                return;
            }

            var keyHash = ApiKeyGenerator.HashKey(apiKey);
            var apiKeyRecord = await db.AgencyApiKeys
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked);

            if (apiKeyRecord == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid or revoked API key." });
                return;
            }

            if (apiKeyRecord.ExpiresAt.HasValue && apiKeyRecord.ExpiresAt.Value < DateTime.UtcNow)
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
            await db.SaveChangesAsync();

            context.Items["AgencyId"] = apiKeyRecord.AgencyId;
            context.Items["ApiKeyName"] = apiKeyRecord.Name;
            context.Items["ApiKeyId"] = apiKeyRecord.Id;

            await _next(context);
        }
    }
}
