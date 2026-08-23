using Confluent.Kafka;

namespace Property.Microservice.Services
{
    public class KafkaPublisherService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ProducerConfig _config;

        public KafkaPublisherService()
        {
            // Configure baseline Kafka developer environment engine connections pointing to docker container cluster
            _config = new ProducerConfig
            {
                BootstrapServers = "localhost:9092",
                AllowAutoCreateTopics = true,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, string>(_config).Build();
        }

        // Asynchronously stream events out to specific messaging topic arrays
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

        // 📨 Standardize naming convention to satisfy controller compiler bindings
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
