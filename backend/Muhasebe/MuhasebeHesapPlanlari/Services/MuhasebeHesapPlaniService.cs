using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using System.Text.Json;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Services;

public class MuhasebeHesapPlaniService : BaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>, IMuhasebeHesapPlaniService
{
    private const string CacheVersionKey = "Muhasebe:HesapPlani:CacheVersion";
    private const string TreeCacheKeyPrefix = "Muhasebe:HesapPlani:Tree";

    private readonly IMuhasebeHesapPlaniRepository _repository;
    private readonly IDistributedCache _distributedCache;

    public MuhasebeHesapPlaniService(IMuhasebeHesapPlaniRepository repository, IMapper mapper, IDistributedCache distributedCache)
        : base(repository, mapper)
    {
        _repository = repository;
        _distributedCache = distributedCache;
    }

    public override async Task<MuhasebeHesapPlaniDto> AddAsync(MuhasebeHesapPlaniDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        var created = await base.AddAsync(dto);
        await InvalidateCacheAsync();
        return created;
    }

    public override async Task<MuhasebeHesapPlaniDto> UpdateAsync(MuhasebeHesapPlaniDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap plani id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        var updated = await base.UpdateAsync(dto);
        await InvalidateCacheAsync();
        return updated;
    }

    public override async Task DeleteAsync(int id)
    {
        await base.DeleteAsync(id);
        await InvalidateCacheAsync();
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default)
        => await GetTreeCachedAsync(cancellationToken);

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeRootsAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _repository.GetRootNodesAsync(cancellationToken);
        return await MapTreeLevelAsync(nodes, cancellationToken);
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeChildrenAsync(int? parentId, CancellationToken cancellationToken = default)
    {
        if (!parentId.HasValue)
        {
            return await GetTreeRootsAsync(cancellationToken);
        }

        var nodes = await _repository.GetChildrenByParentIdAsync(parentId.Value, cancellationToken);
        return await MapTreeLevelAsync(nodes, cancellationToken);
    }

    private async Task<List<MuhasebeHesapPlaniDto>> MapTreeLevelAsync(List<MuhasebeHesapPlani> nodes, CancellationToken cancellationToken)
    {
        var result = new List<MuhasebeHesapPlaniDto>(nodes.Count);
        foreach (var node in nodes)
        {
            var dto = Mapper.Map<MuhasebeHesapPlaniDto>(node);
            dto.HasChildren = await _repository.HasChildrenAsync(node.TamKod, node.SeviyeNo, cancellationToken);
            result.Add(dto);
        }

        return result;
    }

    private async Task<List<MuhasebeHesapPlaniDto>> GetTreeCachedAsync(CancellationToken cancellationToken)
    {
        var version = await GetCacheVersionAsync(cancellationToken);
        var cacheKey = $"{TreeCacheKeyPrefix}:v{version}";
        var payload = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload))
        {
            var cached = JsonSerializer.Deserialize<List<MuhasebeHesapPlaniDto>>(payload);
            if (cached is not null)
            {
                return cached;
            }
        }

        var items = (await _repository.GetAllAsync())
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .Select(x => Mapper.Map<MuhasebeHesapPlaniDto>(x))
            .ToList();

        var serialized = JsonSerializer.Serialize(items);
        await _distributedCache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        }, cancellationToken);

        return items;
    }

    private async Task NormalizeAndValidateAsync(MuhasebeHesapPlaniDto dto, int? currentId)
    {
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
        dto.TamKod = (dto.TamKod ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.TamKod))
        {
            throw new BaseException("Tam kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (dto.SeviyeNo <= 0)
        {
            throw new BaseException("Seviye no 0'dan buyuk olmalidir.", 400);
        }

        if (dto.UstHesapId.HasValue)
        {
            if (currentId.HasValue && dto.UstHesapId.Value == currentId.Value)
            {
                throw new BaseException("Bir hesap kendisinin ust hesabi olamaz.", 400);
            }

            var parentExists = await _repository.AnyAsync(x => x.Id == dto.UstHesapId.Value);
            if (!parentExists)
            {
                throw new BaseException("Secilen ust hesap bulunamadi.", 400);
            }
        }

        var tamKodExists = await _repository.AnyAsync(x =>
            x.TamKod == dto.TamKod && (!currentId.HasValue || x.Id != currentId.Value));
        if (tamKodExists)
        {
            throw new BaseException("Tam kod benzersiz olmalidir.", 400);
        }

        var siblingKodExists = await _repository.AnyAsync(x =>
            x.Kod == dto.Kod
            && x.UstHesapId == dto.UstHesapId
            && (!currentId.HasValue || x.Id != currentId.Value));
        if (siblingKodExists)
        {
            throw new BaseException("Ayni ust hesap altinda kod benzersiz olmalidir.", 400);
        }
    }

    private async Task<string> GetCacheVersionAsync(CancellationToken cancellationToken)
    {
        var version = await _distributedCache.GetStringAsync(CacheVersionKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        const string initialVersion = "1";
        await _distributedCache.SetStringAsync(CacheVersionKey, initialVersion, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
        }, cancellationToken);
        return initialVersion;
    }

    private async Task InvalidateCacheAsync()
    {
        await _distributedCache.SetStringAsync(CacheVersionKey, Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
        });
    }
}
