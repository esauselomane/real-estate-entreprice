using Property.Microservice.DTOs;
using Property.Microservice.Models;

namespace Property.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="PropertyAsset"/> aggregate root.
/// </summary>
public interface IPropertyRepository : IRepository<PropertyAsset>, ITenantScoped<PropertyAsset>
{
}
