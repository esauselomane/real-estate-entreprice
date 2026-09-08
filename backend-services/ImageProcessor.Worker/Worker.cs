using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConsumer<string, string> _kafkaConsumer;
        private readonly IProducer<string, string> _kafkaProducer;
        private readonly string _bootstrapServers;
        private readonly string _storageRootPath;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _storageRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertyMediaVault");
            if (!Directory.Exists(_storageRootPath))
            {
                Directory.CreateDirectory(_storageRootPath);
            }

            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "realestate-image-processor-group",
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
            _kafkaConsumer.Subscribe(TopicNames.PropertyImages);
            _logger.LogInformation("[IMAGE WORKER REBOOT]: Securely polling Kafka topic 'property-images' under net10.0 runtime framework.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var streamingResult = _kafkaConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (streamingResult == null) continue;

                    _logger.LogInformation($"[KAFKA INBOUND SIGNAL]: Caught task envelope matching Key identifier: {streamingResult.Message.Key}");

                    var evt = JsonSerializer.Deserialize<PropertyImageUploadedEvent>(streamingResult.Message.Value, _jsonOptions);
                    if (evt != null && !string.IsNullOrEmpty(evt.Base64Data))
                    {
                        await ProcessAndPersistImageVariantsAsync(evt);
                    }
                    else
                    {
                        _logger.LogWarning("[IMAGE WORKER]: Received event with no Base64Data — skipping.");
                    }

                    _kafkaConsumer.Commit(streamingResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError($"[KAFKA CONSUME ERROR]: Messaging framework log read trace failed: {ex.Error.Reason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[CRITICAL PROCESSING FAILURE]: General memory loop block fault trace: {ex.Message}");
                }
            }
        }

        private async Task ProcessAndPersistImageVariantsAsync(PropertyImageUploadedEvent task)
        {
            _logger.LogInformation("[IMAGE PROCESSING START]: PropertyId={PropertyId} FileName={FileName} DataLength={DataLength}",
                task.PropertyId, task.FileName, task.Base64Data?.Length ?? 0);
            try
            {
                byte[] rawImageBytes = Convert.FromBase64String(task.Base64Data!);

                string targetFolder = Path.Combine(_storageRootPath, $"Property_{task.PropertyId}");
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                string baseName = Path.GetFileNameWithoutExtension(task.FileName);
                string fileExtension = ".jpg";

                using (MemoryStream imageInMemoryStream = new MemoryStream(rawImageBytes))
                using (Image masterImage = Image.Load(imageInMemoryStream))
                {
                    var transformationSizes = new[]
                    {
                        new { Name = "thumb", Width = 150 },
                        new { Name = "medium", Width = 600 },
                        new { Name = "large", Width = 1200 }
                    };

                    foreach (var sizePreset in transformationSizes)
                    {
                        using (Image variantClone = masterImage.Clone(ctx => ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(sizePreset.Width, 0),
                            Mode = ResizeMode.Max
                        })))
                        {
                            string finalOutputPath = Path.Combine(targetFolder, $"{baseName}_{sizePreset.Name}{fileExtension}");
                            await variantClone.SaveAsJpegAsync(finalOutputPath);
                            _logger.LogInformation($"[DISK STORAGE SUCCESS]: Formatted file variant written: {finalOutputPath}");
                        }
                    }
                }

                // Publish completion event
                var completedEvent = new PropertyImageProcessedEvent(
                    PropertyId: task.PropertyId,
                    FileName: task.FileName,
                    ProcessingStatus: "completed",
                    ThumbnailUrl: $"/media/property_{task.PropertyId}/{baseName}_thumb.jpg",
                    MediumUrl: $"/media/property_{task.PropertyId}/{baseName}_medium.jpg",
                    LargeUrl: $"/media/property_{task.PropertyId}/{baseName}_large.jpg",
                    TenantId: task.TenantId
                );

                await PublishProcessedEventAsync(completedEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[TRANSFORMATION SUB-COMPILER ERROR]: Image processing execution failed for property ID {task.PropertyId}. Detail: {ex.Message}");

                var failedEvent = new PropertyImageProcessedEvent(
                    PropertyId: task.PropertyId,
                    FileName: task.FileName,
                    ProcessingStatus: "failed",
                    ThumbnailUrl: null,
                    MediumUrl: null,
                    LargeUrl: null,
                    TenantId: task.TenantId
                );

                await PublishProcessedEventAsync(failedEvent);
            }
        }

        private async Task PublishProcessedEventAsync(PropertyImageProcessedEvent evt)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                var message = new Message<string, string>
                {
                    Key = evt.PropertyId.ToString(),
                    Value = json
                };

                var result = await _kafkaProducer.ProduceAsync(TopicNames.PropertyImagesProcessed, message);
                _logger.LogInformation("[KAFKA]: Published PropertyImageProcessedEvent to {Topic} [Partition: {Partition}] Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA ERROR]: Failed to publish PropertyImageProcessedEvent");
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
