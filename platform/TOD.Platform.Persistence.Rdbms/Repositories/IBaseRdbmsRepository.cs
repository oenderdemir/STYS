using System.Linq.Expressions;
using TOD.Platform.Persistence.Rdbms.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace TOD.Platform.Persistence.Rdbms.Repositories;

public interface IBaseRdbmsRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
{
    Task<TEntity?> GetByIdAsync(TKey id, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<IEnumerable<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<PagedResult<TEntity>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null);

    Task AddAsync(TEntity entity);

    Task SyncAsync(TEntity entity);

    void Update(TEntity entity);

    void Delete(TEntity entity);

    void DeleteWhere(Expression<Func<TEntity, bool>> predicate);

    void DeleteRange(IEnumerable<TEntity> entities);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> UndoDelete(TKey id, CancellationToken cancellationToken = default);
}

public interface IBaseRdbmsRepository<TEntity> : IBaseRdbmsRepository<TEntity, Guid>
    where TEntity : BaseEntity<Guid>
{
}
