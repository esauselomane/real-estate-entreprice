using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="CRMContact"/> aggregate root.
/// </summary>
public interface IContactRepository : IRepository<CRMContact>, ITenantScoped<CRMContact>, IEmailLookup<CRMContact>
{
}
