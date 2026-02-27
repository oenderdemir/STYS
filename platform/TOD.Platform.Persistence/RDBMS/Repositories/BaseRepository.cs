using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TOD.Platform.Persistence.RDBMS.Entities;
using TOD.Platform.Persistence.RDBMS.Paging;

namespace TOD.Platform.Persistence.RDBMS.Repositories;

public class BaseRepository<TEntity, TKey> : IBaseRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    private readonly IMapper _mapper;

    public BaseRepository(DbContext context, IMapper mapper)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
        _mapper = mapper;
    }

    public async Task<TEntity?> GetByIdAsync(TKey id, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        return await query.FirstOrDefaultAsync(x => x.Id.Equals(id));
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        return await query.ToListAsync();
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        return await query.AnyAsync(predicate);
    }

    public IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        return query.Where(predicate);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<PagedResult<TEntity>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null)
    {
        var (pageNumber, pageSize) = request.Normalize();
        IQueryable<TEntity> query = Context.Set<TEntity>();

        if (include is not null)
        {
            query = include(query);
        }

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync();
        query = orderBy is not null
            ? orderBy(query)
            : query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<TEntity>(items, pageNumber, pageSize, totalCount);
    }

    public Task AddAsync(TEntity entity)
    {
        return DbSet.AddAsync(entity).AsTask();
    }

    public void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        DbSet.Remove(entity);
    }

    public void DeleteRange(IEnumerable<TEntity> entities)
    {
        DbSet.RemoveRange(entities);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncAsync(TEntity entity)
    {
        if (!EqualityComparer<TKey>.Default.Equals(entity.Id, default))
        {
            var current = await DbSet.FirstOrDefaultAsync(e => e.Id.Equals(entity.Id));
            if (current is not null)
            {
                _mapper.Map(entity, current);
                current.IsDeleted = false;
                DbSet.Update(current);
                return;
            }
        }

        await DbSet.AddAsync(entity);
    }

    public void DeleteWhere(Expression<Func<TEntity, bool>> predicate)
    {
        var entities = DbSet.Where(predicate).ToList();
        if (entities.Count > 0)
        {
            DbSet.RemoveRange(entities);
        }
    }

    public async Task<TEntity?> UndoDelete(TKey id, CancellationToken cancellationToken = default)
    {
        var item = await Context.Set<TEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        if (item is not null)
        {
            item.IsDeleted = false;
            await Context.SaveChangesAsync(cancellationToken);
        }

        return item;
    }
}

public class BaseRepository<TEntity> : BaseRepository<TEntity, Guid>, IBaseRepository<TEntity>
    where TEntity : BaseEntity<Guid>
{
    public BaseRepository(DbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}
