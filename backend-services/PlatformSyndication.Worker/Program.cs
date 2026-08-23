using PlatformSyndication.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Inject our syndication background task listener directly into the system pipeline
builder.Services.AddHostedService<Worker>();

var app = builder.Build();
app.Run();
