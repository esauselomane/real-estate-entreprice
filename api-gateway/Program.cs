using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
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
