using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="ContactPerson"/> aggregate root.
/// </summary>
public interface IContactPersonRepository : IRepository<ContactPerson>, IAgencyScoped<ContactPerson>
{
}
