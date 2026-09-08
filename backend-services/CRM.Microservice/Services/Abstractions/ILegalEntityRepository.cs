using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="CRMLegalEntity"/> aggregate root.
/// </summary>
public interface ILegalEntityRepository : IRepository<CRMLegalEntity>, ITenantScoped<CRMLegalEntity>
{
}
