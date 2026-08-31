using System.Text.Json;
using Confluent.Kafka;
using PlatformSyndication.Worker.Models;

namespace PlatformSyndication.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConsumer<string, string> _kafkaConsumer;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "realestate-syndication-worker-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _kafkaConsumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Bind the worker application onto the syndication messaging channel
            _kafkaConsumer.Subscribe("property-syndication");
            _logger.LogInformation("[SYNDICATION WORKER REBOOT]: Securely polling Kafka topic 'property-syndication' under net10.0 framework.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Long-poll the message stream queue with a 2-second timeout check loop
                    var streamingResult = _kafkaConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (streamingResult == null) continue;

                    _logger.LogInformation($"[KAFKA INBOUND SYNC]: Caught task envelope matching Key identifier: {streamingResult.Message.Key}");

                    // Parse the serialized message string back into a standard task object structure
                    var syndicationTask = JsonSerializer.Deserialize<SyndicationTask>(streamingResult.Message.Value);
                    if (syndicationTask != null)
                    {
                        // Pass off configuration payload parameters down to the transmission emulator loop
                        await ExecuteExternalPortalTransmissionAsync(syndicationTask);
                    }

                    // Explicitly commit the offset block to signal consumer success queue parameters
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
        // --- 🔗 EXTERNAL PORTAL SYNDICATION TRANSMISSION EMULATOR ---
        private async Task ExecuteExternalPortalTransmissionAsync(SyndicationTask task)
        {
            // Map numerical IDs back to names for reporting outputs
            string platformName = task.PlatformId == 1 ? "Property24" : (task.PlatformId == 2 ? "PrivateProperty" : "Gumtree");
            
            _logger.LogInformation($"[PORTAL INTEGRATION RUN]: Preparing payloads package for backend API on {platformName}.");

            // Simulate high-speed network synchronization lag
            await Task.Delay(1500);

            if (task.IsActive)
            {
                // Emit log replicating an external multipart REST metadata post push payload mutation
                _logger.LogInformation($"[SYNC SUCCESS] >>> Property reference ID [{task.PropertyId}] has been successfully LISTED onto {platformName}. API payload synced.");
            }
            else
            {
                // Emit log replicating an external drop record delete signal
                _logger.LogInformation($"[DE-LIST SUCCESS] <<< Property reference ID [{task.PropertyId}] has been successfully REMOVED from {platformName}. External listing index destroyed.");
            }
        }

        public override void Dispose()
        {
            _kafkaConsumer?.Close();
            _kafkaConsumer?.Dispose();
            base.Dispose();
        }
    }
}
