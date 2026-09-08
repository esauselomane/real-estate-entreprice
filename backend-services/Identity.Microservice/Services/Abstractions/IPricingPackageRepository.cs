using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="PricingPackage"/> aggregate root.
/// </summary>
public interface IPricingPackageRepository : IRepository<PricingPackage>
{
    /// <summary>
    /// Retrieves a pricing package by its code.
    /// </summary>
    Task<PricingPackage?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
