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
            .AllowCredentials();
    });
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors("CorsPolicy");

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

await app.UseOcelot();

app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));

app.Run();
