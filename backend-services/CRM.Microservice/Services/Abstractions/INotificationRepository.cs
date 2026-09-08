using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="Notification"/> aggregate root.
/// </summary>
public interface INotificationRepository : IRepository<Notification>, ITenantScoped<Notification>
{
}
