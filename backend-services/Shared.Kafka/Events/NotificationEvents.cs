namespace Shared.Kafka.Events;

/// <summary>
///   Published by ANY microservice when a user notification should be created.
///   Consumed by CRM.Microservice.
/// </summary>
public record NotificationCreatedEvent(
    string TenantId,
    string UserId,
    string Category,
    string Title,
    string Body,
    string LinkType,
    string LinkId
);

/// <summary>
///   Published by DataImport.Worker when a contact is imported.
///   Consumed by CRM.Microservice.
/// </summary>
public record ContactImportedEvent(
    string TenantId,
    string FullName,
    string Email,
    string Phone,
    string CompanyName,
    string RecordType,
    string PrimaryLocation,
    decimal BudgetLimit,
    string PropertyTypeInterests,
    string AreaInterestIds,
    string Notes
);

/// <summary>
///   Request/Reply pattern for permission checks across services.
///   Published by CRM/Property, consumed by Identity, reply published to PermissionCheckResults.
/// </summary>
public record PermissionCheckRequestedEvent(
    string CorrelationId,
    string TenantId,
    string UserId,
    string Module,
    string Action
);

public record PermissionCheckResultEvent(
    string CorrelationId,
    string TenantId,
    string UserId,
    string Module,
    string Action,
    bool Allowed,
    string? Reason
);
