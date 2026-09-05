using System.Text.Json;
using Confluent.Kafka;
using ImageProcessor.Worker.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConsumer<string, string> _kafkaConsumer;
        private readonly string _storageRootPath;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;

            _storageRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertyMediaVault");
            if (!Directory.Exists(_storageRootPath))
            {
                Directory.CreateDirectory(_storageRootPath);
            }

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "realestate-image-processor-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _kafkaConsumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 3. Bind the application consumer listener onto the images channel
            _kafkaConsumer.Subscribe("property-images");
            _logger.LogInformation("[IMAGE WORKER REBOOT]: Securely polling Kafka topic 'property-images' under net10.0 runtime framework.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Long-poll the message stream container queue with a 2-second timeout check loop
                    var streamingResult = _kafkaConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (streamingResult == null) continue;

                    _logger.LogInformation($"[KAFKA INBOUND SIGNAL]: Caught task envelope matching Key identifier: {streamingResult.Message.Key}");

                    // 4. Parse the serialized message string back into a standard task object structure
                    var imageProcessingTask = JsonSerializer.Deserialize<PropertyImageTask>(streamingResult.Message.Value);
                    if (imageProcessingTask != null && !string.IsNullOrEmpty(imageProcessingTask.Base64Data))
                    {
                        // Pass off configuration payload parameters down to the hardware graphics compiler loop
                        await ProcessAndPersistImageVariantsAsync(imageProcessingTask);
                    }

                    // Explicitly commit the offset block to signal consumer success queue parameters
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
        // --- 🖼️ GRAPHICS PROCESSING ALGORITHM: GENERATE THUMB, MEDIUM, & LARGE VARIANTS ---
        private async Task ProcessAndPersistImageVariantsAsync(PropertyImageTask task)
        {
            _logger.LogInformation("[IMAGE PROCESSING START]: PropertyId={PropertyId} FileName={FileName} DataLength={DataLength}",
                task.PropertyId, task.FileName, task.Base64Data?.Length ?? 0);
            try
            {
                // 1. Convert the transmission Base64 text string safely back into raw image bytes
                byte[] rawImageBytes = Convert.FromBase64String(task.Base64Data);

                // 2. Build tenant/property isolated sub-directory structural trees on local server disk storage
                string targetFolder = Path.Combine(_storageRootPath, $"Property_{task.PropertyId}");
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // Strip down filenames to prevent injection path traversal errors
                string baseName = Path.GetFileNameWithoutExtension(task.FileName);
                string fileExtension = ".jpg"; // Force standard format serialization for optimization

                // 3. Load the byte stream into ImageSharp spatial memory arrays
                using (MemoryStream imageInMemoryStream = new MemoryStream(rawImageBytes))
                using (Image masterImage = Image.Load(imageInMemoryStream))
                {
                    // Define targeted width boundary parameters matching layout configurations
                    var transformationSizes = new[]
                    {
                        new { Name = "thumb", Width = 150 },
                        new { Name = "medium", Width = 600 },
                        new { Name = "large", Width = 1200 }
                    };

                    foreach (var sizePreset in transformationSizes)
                    {
                        // Clone the original graphics image configuration to retain master aspect ratios
                        using (Image variantClone = masterImage.Clone(ctx => ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(sizePreset.Width, 0), // Setting height 0 maintains aspect ratio constraints
                            Mode = ResizeMode.Max
                        })))
                        {
                            // 4. Construct file system paths on disk storage
                            string finalOutputPath = Path.Combine(targetFolder, $"{baseName}_{sizePreset.Name}{fileExtension}");
                            
                            // Commit the physical graphics array bits straight down onto the server disk drive
                            await variantClone.SaveAsJpegAsync(finalOutputPath);
                            _logger.LogInformation($"[DISK STORAGE SUCCESS]: Formatted file variant written: {finalOutputPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[TRANSFORMATION SUB-COMPILER ERROR]: Image processing execution failed for property ID {task.PropertyId}. Detail: {ex.Message}");
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
