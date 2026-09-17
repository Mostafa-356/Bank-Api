using System.Linq.Expressions;
using Bank.Domain.Common;

namespace Bank.Domain.Interfaces;

/// <summary>
/// Generic repository interface for CRUD operations.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void SoftDelete(T entity, string? deletedBy = null);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<(IEnumerable<T> Items, int TotalCount)> ListAsync(Expression<Func<T, bool>>? predicate = null, int page = 1, int pageSize = 10);
    Task<(IEnumerable<T> Items, int TotalCount)> SearchAsync(string searchTerm, int page = 1, int pageSize = 10);
    IQueryable<T> Query();

    // Specification Pattern Methods
    Task<IReadOnlyList<T>> ListAsync(Specifications.ISpecification<T> spec);
    Task<T?> FirstOrDefaultAsync(Specifications.ISpecification<T> spec);
    Task<int> CountAsync(Specifications.ISpecification<T> spec);
}
