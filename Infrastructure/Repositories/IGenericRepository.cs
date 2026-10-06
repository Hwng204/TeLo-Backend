namespace Infrastructure.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity);
}
