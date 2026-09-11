using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CRM.Microservice.Models;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public class ContactImportConsumer : CrmKafkaConsumer<ContactImportedEvent>
{
    public ContactImportConsumer(ILogger<ContactImportConsumer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        : base(logger, serviceProvider, configuration, TopicNames.Contacts, "crm-contact-import-consumer")
    {
    }

    protected override async Task HandleEventAsync(ContactImportedEvent evt, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var dbContext = serviceProvider.GetRequiredService<Data.CRMDbContext>();

        var contact = new Models.Contact
        {
            TenantId = evt.TenantId,
            FullName = evt.FullName,
            EmailAddress = evt.Email,
            PhoneNumber = evt.Phone,
            CompanyName = evt.CompanyName,
            ContactType = Enum.TryParse<ContactType>(evt.RecordType, true, out var parsed) ? parsed : ContactType.Buyer,
            PrimaryLocation = evt.PrimaryLocation,
            BudgetLimit = evt.BudgetLimit,
            PropertyTypeInterests = evt.PropertyTypeInterests,
            AreaInterestIds = evt.AreaInterestIds,
            Notes = evt.Notes,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync(ct);

        Logger.LogInformation("[CONTACT IMPORT CONSUMER]: Created contact '{FullName}' for tenant {TenantId}",
            evt.FullName, evt.TenantId);
    }
}
