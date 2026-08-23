using ImageProcessor.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Inject our long-polling background worker task service directly into the system pipeline
builder.Services.AddHostedService<Worker>();

var app = builder.Build();
app.Run();
