using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Inject the custom ocelot.json switchboard routing layout schema
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 2. Configure loose local development CORS rules allowing the Vite React app to hit the gateway
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                return origin.StartsWith("http://localhost:5173", StringComparison.OrdinalIgnoreCase)
                    || origin.StartsWith("http://127.0.0.1:5173", StringComparison.OrdinalIgnoreCase)
                    || origin.StartsWith("http://localhost:3000", StringComparison.OrdinalIgnoreCase)
                    || origin.StartsWith("http://127.0.0.1:3000", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 3. Register Ocelot's core gateway orchestrator tools
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
// 4. Activate CORS Middleware rules inside the execution pipeline
app.UseCors("CorsPolicy");

// 5. Instruct the application to intercept traffic and run Ocelot middleware
await app.UseOcelot();

// 6. Start the engine application listener
app.Run();
