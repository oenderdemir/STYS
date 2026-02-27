using AutoMapper;
using System.Linq.Expressions;
using TOD.Platform.Persistence.Rdbms.Dto;
using TOD.Platform.Persistence.Rdbms.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Persistence.Rdbms.Services;

public class BaseRdbmsService<TDto, TEntity, TKey> : IBaseRdbmsService<TDto, TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TDto : BaseRdbmsDto<TKey>
    where TKey : struct
{
    protected readonly IBaseRdbmsRepository<TEntity, TKey> Repository;
    protected readonly IMapper Mapper;

    public BaseRdbmsService(IBaseRdbmsRepository<TEntity, TKey> repository, IMapper mapper)
    {
        Repository = repository;
        Mapper = mapper;
    }

    public virtual async Task<IEnumerable<TDto>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        var entities = await Repository.GetAllAsync(include);
        return Mapper.Map<IEnumerable<TDto>>(entities);
    }

    public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        return Repository.AnyAsync(predicate, include);
    }

    public virtual async Task<TDto?> GetByIdAsync(TKey id, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        var entity = await Repository.GetByIdAsync(id, include);
        return Mapper.Map<TDto?>(entity);
    }

    public virtual async Task<PagedResult<TDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null)
    {
        var pagedEntities = await Repository.GetPagedAsync(request, predicate, include, orderBy);
        var mappedItems = Mapper.Map<List<TDto>>(pagedEntities.Items);
        return new PagedResult<TDto>(mappedItems, pagedEntities.PageNumber, pagedEntities.PageSize, pagedEntities.TotalCount);
    }

    public virtual async Task<TDto> AddAsync(TDto dto)
    {
        if (!dto.Id.HasValue && typeof(TKey) == typeof(Guid))
        {
            dto.Id = (TKey)(object)Guid.NewGuid();
        }

        var entity = Mapper.Map<TEntity>(dto);
        await EnrichEntityAsync(dto, entity);

        await Repository.AddAsync(entity);
        await Repository.SaveChangesAsync();

        await OnEntitySavedAsync(entity.Id);

        return dto;
    }

    public virtual async Task<TDto> UpdateAsync(TDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id bos olamaz.");
        }

        var existingEntity = await Repository.GetByIdAsync(dto.Id.Value);
        if (existingEntity is null)
        {
            throw new InvalidOperationException("Guncellenecek veri bulunamadi.");
        }

        existingEntity.IsDeleted = false;
        Mapper.Map(dto, existingEntity);

        await EnrichEntityAsync(dto, existingEntity);
        Repository.Update(existingEntity);
        await Repository.SaveChangesAsync();

        await OnEntitySavedAsync(existingEntity.Id);

        return Mapper.Map<TDto>(existingEntity);
    }

    public virtual async Task DeleteAsync(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new InvalidOperationException("Entity not found");
        }

        Repository.Delete(entity);
        await Repository.SaveChangesAsync();
    }

    public virtual Task<IEnumerable<TDto>> WhereAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
    {
        var entities = Repository.Where(predicate, include);
        return Task.FromResult(Mapper.Map<IEnumerable<TDto>>(entities));
    }

    protected virtual Task EnrichEntityAsync(TDto dto, TEntity entity)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnEntitySavedAsync(TKey id)
    {
        return Task.CompletedTask;
    }
}

public class BaseRdbmsService<TDto, TEntity> : BaseRdbmsService<TDto, TEntity, Guid>, IBaseRdbmsService<TDto, TEntity>
    where TEntity : BaseEntity<Guid>
    where TDto : BaseRdbmsDto<Guid>
{
    public BaseRdbmsService(IBaseRdbmsRepository<TEntity, Guid> repository, IMapper mapper)
        : base(repository, mapper)
    {
    }
}
