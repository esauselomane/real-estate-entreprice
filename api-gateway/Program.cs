using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddJsonFile("ocelot.{environment}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
string environment = builder.Environment.EnvironmentName;

var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"]
    ?? Environment.GetEnvironmentVariable("GATEWAY_BASE_URL")
    ?? "http://gateway:5000";

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
