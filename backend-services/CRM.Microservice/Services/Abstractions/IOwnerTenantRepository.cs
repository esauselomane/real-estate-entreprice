using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="OwnerTenantRecord"/> aggregate root.
/// </summary>
public interface IOwnerTenantRepository : IRepository<OwnerTenantRecord>, ITenantScoped<OwnerTenantRecord>
{
}
