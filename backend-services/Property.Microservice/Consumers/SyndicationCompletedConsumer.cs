using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Confluent.Kafka;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using System.Text.Json;

namespace Property.Microservice.Consumers;

public class SyndicationCompletedConsumer : BackgroundService
{
    private readonly ILogger<SyndicationCompletedConsumer> _logger;
    private readonly string _bootstrapServers;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SyndicationCompletedConsumer(ILogger<SyndicationCompletedConsumer> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "property-syndication-completed-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(TopicNames.PropertySyndicationCompleted);

        _logger.LogInformation("[KAFKA CONSUMER]: Subscribed to topic '{Topic}'", TopicNames.PropertySyndicationCompleted);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                var evt = JsonSerializer.Deserialize<PropertySyndicationCompletedEvent>(result.Message.Value, _jsonOptions);
                if (evt != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    await HandleEventAsync(evt, scope.ServiceProvider, stoppingToken);
                }

                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA CONSUMER]: Error consuming topic '{Topic}'", TopicNames.PropertySyndicationCompleted);
            }
        }
        consumer.Close();
    }

    private async Task HandleEventAsync(PropertySyndicationCompletedEvent evt, IServiceProvider sp, CancellationToken ct)
    {
        // In a full implementation this would update the PropertyPlatformSyndication record
        // with the external listing ID. For now we just log.
        _logger.LogInformation("[SYNDICATION CONSUMER]: PropertyId={PropertyId} PlatformId={PlatformId} Success={Success} ExternalId={ExternalId}",
            evt.PropertyId, evt.PlatformId, evt.Success, evt.ExternalListingId);
    }
}
