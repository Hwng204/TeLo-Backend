using Infrastructure.Context;

namespace Infrastructure.Repositories;

// Changes are only tracked here; IUnitOfWork.CompleteAsync writes them to the database.
public class GenericRepository<T>(ApplicationDbContext db) : IGenericRepository<T> where T : class
{
    protected ApplicationDbContext Db { get; } = db;

    public virtual async Task AddAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        await Db.Set<T>().AddAsync(entity, cancellationToken);
    }

    public virtual Task DeleteAsync(T entity)
    {
        Db.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }
}
