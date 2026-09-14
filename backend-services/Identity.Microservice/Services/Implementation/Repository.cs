using System.Linq.Expressions;
using System.Linq;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Generic repository implementation wrapping Entity Framework Core.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    private readonly IdentityDbContext _dbContext;
    protected readonly DbSet<T> _dbSet;
    protected readonly IdentityDbContext _dbContextProtected;
    private readonly ILogger<Repository<T>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Repository{T}"/> class.
    /// </summary>
    public Repository(IdentityDbContext dbContext, ILogger<Repository<T>> logger)
    {
        _dbContext = dbContext;
        _dbContextProtected = dbContext;
        _dbSet = dbContext.Set<T>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public IQueryable<T> Query()
    {
        return _dbSet;
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    /// <inheritdoc/>
    public void Add(T entity)
    {
        _dbSet.Add(entity);
    }

    /// <inheritdoc/>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
    }

    /// <inheritdoc/>
    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    /// <inheritdoc/>
    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    /// <inheritdoc/>
    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    /// <inheritdoc/>
    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }
}
