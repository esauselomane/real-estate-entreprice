using Confluent.Kafka;

namespace Property.Microservice.Services
{
    public class KafkaPublisherService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ProducerConfig _config;
        private readonly string _bootstrapServers;

        public KafkaPublisherService(IConfiguration configuration)
        {
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            _config = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                AllowAutoCreateTopics = true,
                Acks = Acks.All,
                // 📐 Raise the per-message ceiling to 25 MB so legitimate
                // event payloads (audit logs, large documents) can flow.
                // The broker's `message.max.bytes` MUST also be raised
                // server-side — this just makes the producer match.
                MessageMaxBytes = 25 * 1024 * 1024
            };

            _producer = new ProducerBuilder<string, string>(_config).Build();
        }

        public async Task RaiseEventAsync(string eventTopic, string eventKey, string serializedPayloadJson)
        {
            try
            {
                var outboundEnvelope = new Message<string, string>
                {
                    Key = eventKey,
                    Value = serializedPayloadJson
                };

                var transmissionReceipt = await _producer.ProduceAsync(eventTopic, outboundEnvelope);
                Console.WriteLine($"[KAFKA EVENT LOG]: Event safely committed into {transmissionReceipt.Topic} [Partition: {transmissionReceipt.Partition.Value}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KAFKA CRITICAL ERROR]: Event stream pipeline dispatch failed. Reason: {ex.Message}");
            }
        }

        public async Task PublishMessageAsync(string topicName, string routingKey, string messageValue)
        {
            using (var producer = new ProducerBuilder<string, string>(_config).Build())
            {
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
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
        }
    }
}
