namespace Shared.Kafka.Topics;

/// <summary>
///   Central registry for all Kafka topic names. Use these constants
///   instead of magic strings so producers/consumers stay in sync.
/// </summary>
public static class TopicNames
{
    // 📥 Data import pipeline
    public const string DataImport = "data-import";
    public const string ImportCompleted = "import-completed";

    // 🖼️ Property images
    public const string PropertyImages = "property-images";
    public const string PropertyImagesProcessed = "property-images-processed";

    // 📄 Brochure generation
    public const string BrochureGenerationRequests = "brochure-generation-requests";
    public const string BrochureGenerationCompleted = "brochure-generation-completed";

    // 🌐 Platform syndication
    public const string PropertySyndication = "property-syndication";
    public const string PropertySyndicationCompleted = "property-syndication-completed";

    // 🔔 Cross-service notifications
    public const string Notifications = "notifications";

    // 👤 Contact sync (DataImport.Worker -> CRM)
    public const string Contacts = "contacts";

    // 🛡️ Permission checks (Property/CRM -> Identity)
    public const string PermissionChecks = "permission-checks";
    public const string PermissionCheckResults = "permission-check-results";
}
