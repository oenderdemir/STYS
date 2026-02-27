using System.Linq.Expressions;
using TOD.Platform.Persistence.RDBMS.Entities;

namespace TOD.Platform.Persistence.RDBMS.Repositories;

public interface IBaseRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
{
    Task<TEntity?> GetByIdAsync(TKey id, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<IEnumerable<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task AddAsync(TEntity entity);

    Task SyncAsync(TEntity entity);

    void Update(TEntity entity);

    void Delete(TEntity entity);

    void DeleteWhere(Expression<Func<TEntity, bool>> predicate);

    void DeleteRange(IEnumerable<TEntity> entities);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> UndoDelete(TKey id, CancellationToken cancellationToken = default);
}

public interface IBaseRepository<TEntity> : IBaseRepository<TEntity, Guid>
    where TEntity : BaseEntity<Guid>
{
}
