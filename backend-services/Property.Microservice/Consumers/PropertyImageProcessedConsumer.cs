using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Confluent.Kafka;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using System.Text.Json;

namespace Property.Microservice.Consumers;

public class PropertyImageProcessedConsumer : BackgroundService
{
    private readonly ILogger<PropertyImageProcessedConsumer> _logger;
    private readonly string _bootstrapServers;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PropertyImageProcessedConsumer(ILogger<PropertyImageProcessedConsumer> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "property-image-processed-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(TopicNames.PropertyImagesProcessed);

        _logger.LogInformation("[KAFKA CONSUMER]: Subscribed to topic '{Topic}'", TopicNames.PropertyImagesProcessed);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                var evt = JsonSerializer.Deserialize<PropertyImageProcessedEvent>(result.Message.Value, _jsonOptions);
                if (evt != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    await HandleEventAsync(evt, scope.ServiceProvider, stoppingToken);
                }

                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KAFKA CONSUMER]: Error consuming topic '{Topic}'", TopicNames.PropertyImagesProcessed);
            }
        }
        consumer.Close();
    }

    private async Task HandleEventAsync(PropertyImageProcessedEvent evt, IServiceProvider sp, CancellationToken ct)
    {
        var dbContext = sp.GetRequiredService<PropertyDbContext>();
        var kafkaPublisher = sp.GetRequiredService<Services.KafkaPublisherService>();

        var property = await dbContext.PropertyAssets.FirstOrDefaultAsync(p => p.PropertyId == evt.PropertyId, ct);
        if (property == null)
        {
            _logger.LogWarning("[IMAGE PROCESSED CONSUMER]: Property {PropertyId} not found", evt.PropertyId);
            return;
        }

        // Update the property's image gallery: replace the raw data URL with the processed file URLs
        var current = string.IsNullOrWhiteSpace(property.GalleryImageJson)
            ? new List<string>()
            : (JsonSerializer.Deserialize<List<string>>(property.GalleryImageJson) ?? new List<string>());

        // Remove the original data URL (the one that starts with "data:")
        current.RemoveAll(url => url.StartsWith("data:", StringComparison.OrdinalIgnoreCase));

        // Add the processed URLs
        var processedUrls = new[]
        {
            evt.ThumbnailUrl,
            evt.MediumUrl,
            evt.LargeUrl
        }.Where(url => !string.IsNullOrEmpty(url)).ToList();

        current.AddRange(processedUrls);
        if (current.Count > 50) current = current.GetRange(current.Count - 50, 50);

        property.GalleryImageJson = JsonSerializer.Serialize(current);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("[IMAGE PROCESSED CONSUMER]: Updated Property {PropertyId} with {Count} processed image URLs", evt.PropertyId, current.Count);

        // Publish notification event
        var notificationEvent = new NotificationCreatedEvent(
            TenantId: evt.TenantId,
            UserId: "",
            Category: "property",
            Title: "Image processed",
            Body: $"Image for property {evt.PropertyId} has been processed successfully.",
            LinkType: "property",
            LinkId: evt.PropertyId.ToString()
        );

        try
        {
            await kafkaPublisher.RaiseEventAsync(TopicNames.Notifications, evt.PropertyId.ToString(), notificationEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IMAGE PROCESSED CONSUMER]: Failed to publish notification event for Property {PropertyId}", evt.PropertyId);
        }
    }
}