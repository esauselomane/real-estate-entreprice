using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddJsonFile("ocelot.json", optional: true, reloadOnChange: true);
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

var downstreamScheme = Environment.GetEnvironmentVariable("DOWNSTREAM_SCHEME") ?? "http";

var ocelotJson = $@"{{
  ""Routes"": [
    {{
      ""DownstreamPathTemplate"": ""/auth/{{everything}}"",
      ""DownstreamScheme"": ""{downstreamScheme}"",
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
      ""DownstreamScheme"": ""{downstreamScheme}"",
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
      ""DownstreamScheme"": ""{downstreamScheme}"",
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
      ""DownstreamPathTemplate"": ""/system/{{everything}}"",
      ""DownstreamScheme"": ""{downstreamScheme}"",
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
      ""DownstreamPathTemplate"": ""/payments/{{everything}}"",
      ""DownstreamScheme"": ""{downstreamScheme}"",
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
      ""DownstreamScheme"": ""{downstreamScheme}"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{developerHost}"",
          ""Port"": {developerPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/developer/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/user/{{everything}}"",
      ""DownstreamScheme"": ""{downstreamScheme}"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{identityHost}"",
          ""Port"": {identityPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/user/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"", ""Post"", ""Put"", ""Delete"" ]
    }},
    {{
      ""DownstreamPathTemplate"": ""/profiles/{{everything}}"",
      ""DownstreamScheme"": ""{downstreamScheme}"",
      ""DownstreamHostAndPorts"": [
        {{
          ""Host"": ""{identityHost}"",
          ""Port"": {identityPort}
        }}
      ],
      ""UpstreamPathTemplate"": ""/gateway/profiles/{{everything}}"",
      ""UpstreamHttpMethod"": [ ""Get"" ]
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
    ["Routes:6:DownstreamHostAndPorts:0:Host"] = identityHost,
    ["Routes:6:DownstreamHostAndPorts:0:Port"] = identityPort,
    ["Routes:7:DownstreamHostAndPorts:0:Host"] = identityHost,
    ["Routes:7:DownstreamHostAndPorts:0:Port"] = identityPort,
    ["GlobalConfiguration:BaseUrl"] = gatewayBaseUrl
});

builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(ocelotJson)));

var allowedOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? builder.Configuration["Cors:AllowedOrigins"] ?? "https://app.biscovetech.com,https://www.biscovetech.com,http://localhost:5173,http://localhost:3000")
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
          .WithExposedHeaders("X-Aggregates", "X-Deal-Count", "X-Lead-Count", "X-Total-Count", "Content-Disposition");
    });
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.Map("/health", healthApp => {
    healthApp.Run(async context => {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(new { status = "healthy", service = "gateway" });
    });
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
app.Run();
