using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddEnvironmentVariables();

var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"]
    ?? Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
    ?? "http://gateway:5000";

var identityHost = Environment.GetEnvironmentVariable("IDENTITY_HOST") ?? "identity";
var identityPort = Environment.GetEnvironmentVariable("IDENTITY_PORT") ?? "5001";
var propertyHost = Environment.GetEnvironmentVariable("PROPERTY_HOST") ?? "property";
var propertyPort = Environment.GetEnvironmentVariable("PROPERTY_PORT") ?? "5002";
var crmHost = Environment.GetEnvironmentVariable("CRM_HOST") ?? "crm";
var crmPort = Environment.GetEnvironmentVariable("CRM_PORT") ?? "5003";
var developerHost = Environment.GetEnvironmentVariable("DEVELOPER_HOST") ?? "developer-api";
var developerPort = Environment.GetEnvironmentVariable("DEVELOPER_PORT") ?? "5006";

var ocelotJson = $@"{{
  ""Routes"": [
    {{
      ""DownstreamPathTemplate"": ""/api/auth/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{identityHost}"",
          ""Port"": {identityPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/auth/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/api/properties/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{propertyHost}"",
          ""Port"": {propertyPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/properties/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/api/crm/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{crmHost}"",
          ""Port"": {crmPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/crm/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/api/system/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{identityHost}"",
          ""Port"": {identityPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/system/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/api/payments/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{identityHost}"",
          ""Port"": {identityPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/payments/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/api/developer/{{everything}}"",
      ""DownstreamScheme"": ""http"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{developerHost}"",
          ""Port"": {developerPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/developer/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }}
  ],
  ""GlobalConfiguration"": {{
    ""BaseUrl"": ""{gatewayBaseUrl}""
  }}
}}";

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Routes:0:DownstreamHostAndPorts:0:Host"] = identityHost,
    ["Routes:0:DownstreamHostAndPorts:0:Port"] = identityPort,
    ["Routes:1:DownstreamHostAndPorts:0:Host"] = propertyHost,
    ["Routes:1:DownstreamHostAndPorts:0:Port"] = propertyPort,
    ["Routes:2:DownstreamHostAndPorts:0:Host"] = crmHost,
    ["Routes:2:DownstreamHostAndPorts:0:Port"] = crmPort,
    ["Routes:3:DownstreamHostAndPorts:0:Host"] = identityHost,
    ["Routes:3:DownstreamHostAndPorts:0:Port"] = identityPort,
    ["Routes:4:DownstreamHostAndPorts:0:Host"] = identityHost,
    ["Routes:4:DownstreamHostAndPorts:0:Port"] = identityPort,
    ["Routes:5:DownstreamHostAndPorts:0:Host"] = developerHost,
    ["Routes:5:DownstreamHostAndPorts:0:Port"] = developerPort,
    ["GlobalConfiguration:BaseUrl"] = gatewayBaseUrl
});

builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(ocelotJson)));

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "https://realestate.example,https://www.realestate.example,http://localhost:5173,http://localhost:3000")
  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
          .WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()
          // 🪟 Expose every custom response header we set on the downstream
          // services (X-Aggregates, X-Deal-Count, X-Lead-Count, X-Total-Count).
          // Without this, the browser hides them from JS even though the
          // network panel shows them — which makes server-side aggregates
          // (dealStage / leadStage counts across the full universe) silently
          // disappear into `{}` on the client.
          .WithExposedHeaders("X-Aggregates", "X-Deal-Count", "X-Lead-Count", "X-Total-Count", "Content-Disposition");
    });
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// 🪟 CORS header injection that works with Ocelot's proxied responses.
// Ocelot fully writes the downstream response, so mutating
// `context.Response.Headers[...]` AFTER `await next()` throws
// "Headers are read-only" or — worse — corrupts the response stream
// mid-flight and the browser sees a `Network error` even though the
// server returned 200. The safe pattern is to register an
// `OnStarting` callback BEFORE the downstream pipeline runs; the
// callback fires the instant ASP.NET is about to flush headers, at
// which point they're still mutable.
const string EXPOSED_HEADERS = "X-Aggregates, X-Deal-Count, X-Lead-Count, X-Total-Count, Content-Disposition";
app.Use(async (context, next) =>
{
  var origin = context.Request.Headers.Origin.ToString();
  if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
  {
    context.Response.OnStarting(() =>
    {
      var headers = context.Response.Headers;
      headers["Access-Control-Allow-Origin"] = origin;
      headers["Access-Control-Allow-Credentials"] = "true";
      headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
      headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
      headers["Access-Control-Expose-Headers"] = EXPOSED_HEADERS;
      return Task.CompletedTask;
    });
  }
  await next();
});

app.Use(async (context, next) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
  var method = context.Request.Method;
  var path = context.Request.Path;
  var traceId = context.TraceIdentifier;
  logger.LogInformation("Request started: {Method} {Path} TraceId={TraceId}", method, path, traceId);

  await next();

  var statusCode = context.Response.StatusCode;
  var level = statusCode >= 500 ? LogLevel.Error : statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
  logger.Log(level, "Request completed: {Method} {Path} TraceId={TraceId} Status={Status}", method, path, traceId, statusCode);
});

app.UseCors("CorsPolicy");
await app.UseOcelot();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));
app.Run();
