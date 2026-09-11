using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="Contact"/> aggregate root.
/// Consolidates CRM contacts, agency contact persons, and owner/tenant records
/// into a single unified entity distinguished by <see cref="Contact.ContactType"/>.
/// </summary>
public interface IContactRepository : IRepository<Contact>, ITenantScoped<Contact>, IEmailLookup<Contact>
{
}
