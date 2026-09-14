namespace Property.Microservice.Services.BackgroundJobs
{
    /// <summary>
    /// Image processing job - replaces PropertyImageUploadedEvent from Kafka.
    /// </summary>
    public record ImageProcessingJob(
        int PropertyId,
        string FileName,
        string Base64Data,
        string TenantId,
        string RequestedBy
    );

    /// <summary>
    /// Result of image processing job.
    /// </summary>
    public record ImageProcessingResult(
        int PropertyId,
        string FileName,
        string ProcessingStatus,
        string? ThumbnailUrl,
        string? MediumUrl,
        string? LargeUrl,
        string TenantId
    );

    /// <summary>
    /// Syndication job - replaces PropertySyndicationRequestedEvent from Kafka.
    /// </summary>
    public record SyndicationJob(
        int PropertyId,
        int PlatformId,
        bool IsActive,
        string TenantId,
        string RequestedBy
    );

    /// <summary>
    /// Result of syndication job.
    /// </summary>
    public record SyndicationResult(
        int PropertyId,
        int PlatformId,
        bool Success,
        string? ExternalListingId,
        string TenantId
    );

    /// <summary>
    /// Data import job - replaces DataImport Kafka events.
    /// </summary>
    public record DataImportJob(
        string JobId,
        string ImportType,
        string FileName,
        string Base64Content,
        string TenantId,
        string RequestedBy
    );

    /// <summary>
    /// Result of data import job.
    /// </summary>
    public record DataImportResult(
        string Status,
        string Message,
        int SuccessCount,
        int ErrorCount,
        List<string> Errors
    );
}