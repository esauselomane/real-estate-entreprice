using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public abstract class CrmKafkaConsumer<TEvent> : BackgroundService where TEvent : class
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _bootstrapServers;
    private readonly string _topic;
    private readonly string _groupId;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected CrmKafkaConsumer(ILogger logger, IServiceProvider serviceProvider, IConfiguration configuration, string topic, string groupId)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _topic = topic;
        _groupId = groupId;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        _logger.LogInformation("[KAFKA CONSUMER]: Subscribed to topic '{Topic}' (group: {GroupId})", _topic, _groupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result == null) continue;

                var json = result.Message.Value;
                _logger.LogInformation("[KAFKA CONSUMER]: Received message on '{Topic}' key={Key}", _topic, result.Message.Key);

                try
                {
                    var evt = JsonSerializer.Deserialize<TEvent>(json, _jsonOptions);
                    if (evt == null)
                    {
                        _logger.LogWarning("[KAFKA CONSUMER]: Deserialized null event from topic '{Topic}'", _topic);
                        consumer.StoreOffset(result);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    await HandleEventAsync(evt, scope.ServiceProvider, stoppingToken);
                    consumer.StoreOffset(result);
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "[KAFKA CONSUMER]: Schema validation failed for topic '{Topic}'. Payload: {Payload}", _topic, json);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "[KAFKA CONSUMER]: Kafka consume error on topic '{Topic}'", _topic);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA CONSUMER]: Processing error on topic '{Topic}'", _topic);
            }
        }

        consumer.Close();
    }

    protected ILogger Logger => _logger;

    protected abstract Task HandleEventAsync(TEvent evt, IServiceProvider serviceProvider, CancellationToken ct);
}
