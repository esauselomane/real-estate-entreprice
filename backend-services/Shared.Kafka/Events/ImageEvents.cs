namespace Shared.Kafka.Events;

/// <summary>
///   Published by Property.Microservice when an image is uploaded.
///   Consumed by ImageProcessor.Worker.
/// </summary>
public record PropertyImageUploadedEvent(
    int PropertyId,
    string FileName,
    string? Base64Data,
    string TenantId,
    string RequestedBy
);

/// <summary>
///   Published by ImageProcessor.Worker when image processing finishes.
///   Consumed by Property.Microservice.
/// </summary>
public record PropertyImageProcessedEvent(
    int PropertyId,
    string FileName,
    string ProcessingStatus,
    string? ThumbnailUrl,
    string? MediumUrl,
    string? LargeUrl,
    string TenantId
);
