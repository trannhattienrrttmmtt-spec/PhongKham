using System.Linq.Expressions;

namespace PhongKham.Repositories;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null);
    Task<T?> FindAsync(int id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync();
}
