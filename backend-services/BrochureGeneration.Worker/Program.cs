using BrochureGeneration.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BrochureKafkaConsumerWorker>();

var host = builder.Build();
host.Run();


