using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="OperationalRecord"/> aggregate root.
/// </summary>
public interface IOperationalRecordRepository : IRepository<OperationalRecord>, ITenantScoped<OperationalRecord>
{
}
