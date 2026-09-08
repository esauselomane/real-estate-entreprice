using System.Linq.Expressions;
using System.Linq;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace Property.Microservice.Services.Abstractions;

/// <summary>
/// Generic repository contract for asynchronous CRUD operations.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Returns a queryable surface for building custom LINQ queries.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>
    /// Retrieves an entity by its integer primary key.
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its string primary key.
    /// </summary>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities of type T.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the specified predicate.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any entity matches the specified predicate.
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the specified predicate.
    /// </summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    void Add(T entity);

    /// <summary>
    /// Adds a new entity asynchronously.
    /// </summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a range of new entities.
    /// </summary>
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Adds a range of new entities asynchronously.
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entity.
    /// </summary>
    void Remove(T entity);

    /// <summary>
    /// Removes a range of entities.
    /// </summary>
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    void Update(T entity);
}
