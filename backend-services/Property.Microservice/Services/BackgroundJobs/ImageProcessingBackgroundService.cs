using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Property.Microservice.Data;
using Property.Microservice.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Property.Microservice.Services.BackgroundJobs
{
    public class ImageProcessingBackgroundService : BackgroundService
    {
        private readonly ILogger<ImageProcessingBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IJobQueue<ImageProcessingJob> _jobQueue;
        private readonly string _storageRootPath;

        public ImageProcessingBackgroundService(
            ILogger<ImageProcessingBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IJobQueue<ImageProcessingJob> jobQueue,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobQueue = jobQueue;
            _storageRootPath = configuration["Images:StoragePath"] 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertyMediaVault");
        }

        public async ValueTask EnqueueAsync(ImageProcessingJob job, CancellationToken ct = default)
        {
            await _jobQueue.EnqueueAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[IMAGE PROCESSING BACKGROUND SERVICE]: Started, listening for image processing jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("[IMAGE PROCESSING START]: PropertyId={PropertyId} FileName={FileName} DataLength={DataLength}",
                        job.PropertyId, job.FileName, job.Base64Data?.Length ?? 0);

                    var result = await ProcessAndPersistImageVariantsAsync(job, stoppingToken);

                    // Optionally update PropertyAsset or PropertyUnit with the image URLs
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();

                    // Try to find the property asset and add to its gallery
                    var asset = await dbContext.PropertyAssets.FirstOrDefaultAsync(a => a.PropertyId == job.PropertyId, stoppingToken);
                    if (asset != null)
                    {
                        var gallery = System.Text.Json.JsonSerializer.Deserialize<List<string>>(asset.GalleryImageJson) ?? new List<string>();
                        if (!gallery.Contains(result.MediumUrl))
                        {
                            gallery.Add(result.MediumUrl);
                            asset.GalleryImageJson = System.Text.Json.JsonSerializer.Serialize(gallery);
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }

                    _logger.LogInformation("[IMAGE PROCESSING]: Completed PropertyId={PropertyId} Status={Status}", job.PropertyId, result.ProcessingStatus);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[IMAGE PROCESSING BACKGROUND SERVICE]: Error processing job");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task<ImageProcessingResult> ProcessAndPersistImageVariantsAsync(ImageProcessingJob task, CancellationToken ct)
        {
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
                            await variantClone.SaveAsJpegAsync(finalOutputPath, ct);
                            _logger.LogInformation("[DISK STORAGE SUCCESS]: Formatted file variant written: {Path}", finalOutputPath);
                        }
                    }
                }

                return new ImageProcessingResult(
                    PropertyId: task.PropertyId,
                    FileName: task.FileName,
                    ProcessingStatus: "completed",
                    ThumbnailUrl: $"/media/property_{task.PropertyId}/{baseName}_thumb.jpg",
                    MediumUrl: $"/media/property_{task.PropertyId}/{baseName}_medium.jpg",
                    LargeUrl: $"/media/property_{task.PropertyId}/{baseName}_large.jpg",
                    TenantId: task.TenantId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[IMAGE PROCESSING ERROR]: Image processing execution failed for property ID {PropertyId}", task.PropertyId);

                return new ImageProcessingResult(
                    PropertyId: task.PropertyId,
                    FileName: task.FileName,
                    ProcessingStatus: "failed",
                    ThumbnailUrl: null,
                    MediumUrl: null,
                    LargeUrl: null,
                    TenantId: task.TenantId
                );
            }
        }
    }
}