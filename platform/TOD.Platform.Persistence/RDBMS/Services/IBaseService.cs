using System.Linq.Expressions;
using TOD.Platform.Persistence.RDBMS.Dto;
using TOD.Platform.Persistence.RDBMS.Entities;
using TOD.Platform.Persistence.RDBMS.Paging;

namespace TOD.Platform.Persistence.RDBMS.Services;

public interface IBaseService<TDto, TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TDto : BaseRdbmsDto<TKey>
    where TKey : struct
{
    Task<IEnumerable<TDto>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<TDto?> GetByIdAsync(TKey id, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<PagedResult<TDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null);

    Task<TDto> AddAsync(TDto dto);

    Task<TDto> UpdateAsync(TDto dto);

    Task DeleteAsync(TKey id);

    Task<IEnumerable<TDto>> WhereAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);
}

public interface IBaseService<TDto, TEntity> : IBaseService<TDto, TEntity, Guid>
    where TEntity : BaseEntity<Guid>
    where TDto : BaseRdbmsDto<Guid>
{
}
