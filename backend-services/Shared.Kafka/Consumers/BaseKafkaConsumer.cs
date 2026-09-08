using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Kafka.Consumers;

/// <summary>
///   Base class for Kafka consumers. Handles configuration, deserialization,
///   and graceful shutdown.
/// </summary>
/// <typeparam name="TEvent">The strongly-typed event this consumer handles.</typeparam>
public abstract class BaseKafkaConsumer<TEvent> : BackgroundService where TEvent : class
{
    private readonly ILogger _logger;
    private readonly string _bootstrapServers;
    private readonly string _topic;
    private readonly string _groupId;

    protected BaseKafkaConsumer(ILogger logger, IConfiguration configuration, string topic, string groupId)
    {
        _logger = logger;
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
                    var evt = JsonSerializer.Deserialize<TEvent>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });

                    if (evt == null)
                    {
                        _logger.LogWarning("[KAFKA CONSUMER]: Deserialized null event from topic '{Topic}'", _topic);
                        consumer.StoreOffset(result);
                        continue;
                    }

                    await HandleEventAsync(evt, stoppingToken);
                    consumer.StoreOffset(result);
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "[KAFKA CONSUMER]: Schema validation failed for topic '{Topic}'. Payload: {Payload}", _topic, json);
                    // Do not store offset — message will be redelivered after fix.
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

    /// <summary>
    ///   Implement this method to process the deserialized event.
    ///   Throw if processing fails; the base class will log but NOT commit the offset,
    ///   so the message is redelivered.
    /// </summary>
    protected abstract Task HandleEventAsync(TEvent evt, CancellationToken ct);
}
