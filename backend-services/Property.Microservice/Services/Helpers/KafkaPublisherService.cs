using System.Text.Json;
using Confluent.Kafka;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace Property.Microservice.Services
{
    public class KafkaPublisherService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ProducerConfig _config;
        private readonly string _bootstrapServers;
        private readonly JsonSerializerOptions _jsonOptions;

        public KafkaPublisherService(IConfiguration configuration)
        {
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            _config = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                AllowAutoCreateTopics = true,
                Acks = Acks.All,
                MessageMaxBytes = 25 * 1024 * 1024
            };

            _producer = new ProducerBuilder<string, string>(_config).Build();
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task RaiseEventAsync<TEvent>(string topic, string key, TEvent evt)
        {
            await RaiseEventAsync(topic, key, evt, maxRetries: 3, initialDelayMs: 1000);
        }

        /// <summary>
        /// Publish a Kafka event with exponential-backoff retry.
        /// Retries up to maxRetries times with increasing delays so that a
        /// temporarily unavailable broker (e.g. during startup) does not
        /// permanently lose the event.
        /// </summary>
        public async Task RaiseEventAsync<TEvent>(
            string topic,
            string key,
            TEvent evt,
            int maxRetries = 3,
            int initialDelayMs = 1000)
        {
            var json = JsonSerializer.Serialize(evt, _jsonOptions);
            var message = new Message<string, string> { Key = key, Value = json };

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var receipt = await _producer.ProduceAsync(topic, message);
                    Console.WriteLine($"[KAFKA EVENT LOG]: {typeof(TEvent).Name} -> {receipt.Topic} [Partition: {receipt.Partition}] Offset: {receipt.Offset}");
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(initialDelayMs * (int)Math.Pow(2, attempt));
                    Console.WriteLine($"[KAFKA RETRY]: {typeof(TEvent).Name} attempt {attempt + 1}/{maxRetries + 1} failed. Reason: {ex.Message}. Retrying in {delay.TotalSeconds}s...");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[KAFKA CRITICAL ERROR]: Event stream pipeline dispatch failed after {maxRetries + 1} attempts. Reason: {ex.Message}");
                }
            }
        }

        public async Task PublishMessageAsync(string topicName, string routingKey, string messageValue)
        {
            using var producer = new ProducerBuilder<string, string>(_config).Build();
            try
            {
                var deliveryResult = await producer.ProduceAsync(topicName, new Message<string, string>
                {
                    Key = routingKey,
                    Value = messageValue
                });

                Console.WriteLine($"[KAFKA BUS LOG]: Event successfully streamed to {deliveryResult.TopicPartitionOffset}");
            }
            catch (ProduceException<string, string> ex)
            {
                Console.WriteLine($"[KAFKA ERROR]: Failed to push transactional message event. Reason: {ex.Error.Reason}");
                throw;
            }
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
        }
    }
}
