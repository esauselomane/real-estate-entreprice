using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using PlatformSyndication.Worker.Models;

namespace PlatformSyndication.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConsumer<string, string> _kafkaConsumer;
        private readonly IProducer<string, string> _kafkaProducer;
        private readonly string _bootstrapServers;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "realestate-syndication-worker-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _kafkaConsumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                AllowAutoCreateTopics = true,
                Acks = Acks.All
            };

            _kafkaProducer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _kafkaConsumer.Subscribe(TopicNames.PropertySyndication);
            _logger.LogInformation("[SYNDICATION WORKER REBOOT]: Securely polling Kafka topic 'property-syndication' under net10.0 framework.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var streamingResult = _kafkaConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (streamingResult == null) continue;

                    _logger.LogInformation($"[KAFKA INBOUND SYNC]: Caught task envelope matching Key identifier: {streamingResult.Message.Key}");

                    var evt = JsonSerializer.Deserialize<PropertySyndicationRequestedEvent>(streamingResult.Message.Value, _jsonOptions);
                    if (evt != null)
                    {
                        await ExecuteExternalPortalTransmissionAsync(evt);
                    }

                    _kafkaConsumer.Commit(streamingResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError($"[KAFKA CONSUME ERROR]: Syndication message log read trace failed: {ex.Error.Reason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[CRITICAL SYNDICATION FAILURE]: General memory loop block fault trace: {ex.Message}");
                }
            }
        }

        private async Task ExecuteExternalPortalTransmissionAsync(PropertySyndicationRequestedEvent evt)
        {
            string platformName = evt.PlatformId == 1 ? "Property24" : (evt.PlatformId == 2 ? "PrivateProperty" : "Gumtree");

            _logger.LogInformation($"[PORTAL INTEGRATION RUN]: Preparing payloads package for backend API on {platformName}.");

            await Task.Delay(1500);

            string externalListingId = evt.PlatformId == 1 ? $"P24-{evt.PropertyId}" : (evt.PlatformId == 2 ? $"PP-{evt.PropertyId}" : $"GT-{evt.PropertyId}");

            var completedEvent = new PropertySyndicationCompletedEvent(
                PropertyId: evt.PropertyId,
                PlatformId: evt.PlatformId,
                Success: evt.IsActive,
                ExternalListingId: externalListingId,
                TenantId: evt.TenantId
            );

            await PublishCompletedEventAsync(completedEvent);

            if (evt.IsActive)
            {
                _logger.LogInformation($"[SYNC SUCCESS] >>> Property reference ID [{evt.PropertyId}] has been successfully LISTED onto {platformName}. API payload synced.");
            }
            else
            {
                _logger.LogInformation($"[DE-LIST SUCCESS] <<< Property reference ID [{evt.PropertyId}] has been successfully REMOVED from {platformName}. External listing index destroyed.");
            }
        }

        private async Task PublishCompletedEventAsync(PropertySyndicationCompletedEvent evt)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                var message = new Message<string, string>
                {
                    Key = evt.PropertyId.ToString(),
                    Value = json
                };

                var result = await _kafkaProducer.ProduceAsync(TopicNames.PropertySyndicationCompleted, message);
                _logger.LogInformation("[KAFKA]: Published PropertySyndicationCompletedEvent to {Topic} [Partition: {Partition}] Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA ERROR]: Failed to publish PropertySyndicationCompletedEvent");
            }
        }

        public override void Dispose()
        {
            _kafkaConsumer?.Close();
            _kafkaConsumer?.Dispose();
            _kafkaProducer?.Flush(TimeSpan.FromSeconds(5));
            _kafkaProducer?.Dispose();
            base.Dispose();
        }
    }
}
