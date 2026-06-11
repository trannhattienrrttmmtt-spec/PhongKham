using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using System.Linq.Expressions;

namespace PhongKham.Repositories;

public class EfRepository<T>(ClinicDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>().AsQueryable();

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        var query = Query();
        return (predicate is null ? query : query.Where(predicate)).ToListAsync();
    }

    public Task<T?> FindAsync(int id) => db.Set<T>().FindAsync(id).AsTask();

    public async Task AddAsync(T entity)
    {
        await db.Set<T>().AddAsync(entity);
    }

    public void Update(T entity)
    {
        db.Set<T>().Update(entity);
    }

    public void Remove(T entity)
    {
        db.Set<T>().Remove(entity);
    }

    public Task<int> SaveChangesAsync() => db.SaveChangesAsync();
}
