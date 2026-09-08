namespace Shared.Kafka.Events;

/// <summary>
///   Published by Property.Microservice when a new import job is accepted.
///   Consumed by DataImport.Worker.
/// </summary>
public record ImportJobReceivedEvent(
    string JobId,
    string ImportType,
    string FileName,
    string RequestedBy,
    string TenantId,
    DateTime CreatedAt
);

/// <summary>
///   Published by DataImport.Worker when an import job finishes.
///   Consumed by CRM.Microservice (to create notification) and by the Property
///   service (to update job status).
/// </summary>
public record ImportJobCompletedEvent(
    string JobId,
    string ImportType,
    int SuccessCount,
    int ErrorCount,
    List<string> Errors,
    string TenantId,
    string RequestedBy,
    string Status,
    DateTime CompletedAt
);
