using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using STYS.Muhasebe.TasinirKodlari.Dtos;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TasinirKodlari.Services;

public class TasinirKodService : BaseRdbmsService<TasinirKodDto, TasinirKod, int>, ITasinirKodService
{
    private const string CacheVersionKey = "Muhasebe:TasinirKodlari:CacheVersion";
    private const string LookupCachePrefix = "Muhasebe:TasinirKodlari:Lookup";

    private readonly ITasinirKodRepository _repository;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _memoryCache;

    public TasinirKodService(ITasinirKodRepository repository, IMapper mapper, IMemoryCache memoryCache)
        : base(repository, mapper)
    {
        _repository = repository;
        _mapper = mapper;
        _memoryCache = memoryCache;
    }

    public override async Task<TasinirKodDto> AddAsync(TasinirKodDto dto)
    {
        NormalizeAndValidate(dto);
        await ValidateUniqueAsync(dto.TamKod, null);
        var created = await base.AddAsync(dto);
        InvalidateLookupCache();
        return created;
    }

    public override async Task<TasinirKodDto> UpdateAsync(TasinirKodDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tasinir kod id zorunludur.", 400);
        }

        NormalizeAndValidate(dto);
        await ValidateUniqueAsync(dto.TamKod, dto.Id.Value);
        var updated = await base.UpdateAsync(dto);
        InvalidateLookupCache();
        return updated;
    }

    public override async Task DeleteAsync(int id)
    {
        await base.DeleteAsync(id);
        InvalidateLookupCache();
    }

    public async Task<PagedResult<TasinirKodDto>> GetPagedForLookupAsync(PagedRequest request, string? query, CancellationToken cancellationToken = default)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var cacheKey = BuildLookupCacheKey(pageNumber, pageSize, normalizedQuery);

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);

            var allItems = await GetAllForLookupCachedAsync(cancellationToken);
            var filtered = ApplyLookupFilter(allItems, normalizedQuery);
            var totalCount = filtered.Count;
            var items = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<TasinirKodDto>(items, pageNumber, pageSize, totalCount);
        }) ?? new PagedResult<TasinirKodDto>([], pageNumber, pageSize, 0);
    }

    public async Task<TasinirKodImportSonucDto> ImportAsync(ImportTasinirKodlariRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Satirlar.Count == 0)
        {
            throw new BaseException("Import satirlari bos olamaz.", 400);
        }

        var normalizedRows = request.Satirlar
            .Where(x => !string.IsNullOrWhiteSpace(x.TamKod) && !string.IsNullOrWhiteSpace(x.Ad))
            .Select(x => new
            {
                TamKod = x.TamKod.Trim(),
                Duzey1Kod = NormalizeOptional(x.Duzey1Kod),
                Duzey2Kod = NormalizeOptional(x.Duzey2Kod),
                Duzey3Kod = NormalizeOptional(x.Duzey3Kod),
                Duzey4Kod = NormalizeOptional(x.Duzey4Kod),
                Duzey5Kod = NormalizeOptional(x.Duzey5Kod),
                Ad = x.Ad.Trim(),
                x.DuzeyNo,
                UstTamKod = NormalizeOptional(x.UstTamKod),
                x.AktifMi,
                Aciklama = NormalizeOptional(x.Aciklama)
            })
            .GroupBy(x => x.TamKod, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var existing = await _repository.GetByTamKodlarAsync(normalizedRows.Select(x => x.TamKod), cancellationToken);
        var existingByTamKod = existing.ToDictionary(x => x.TamKod, StringComparer.OrdinalIgnoreCase);
        var allByTamKod = existingByTamKod.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        var sonuc = new TasinirKodImportSonucDto
        {
            ToplamIslenen = normalizedRows.Count
        };

        foreach (var row in normalizedRows)
        {
            if (existingByTamKod.TryGetValue(row.TamKod, out var current))
            {
                if (!request.MevcutlariGuncelle)
                {
                    continue;
                }

                current.Duzey1Kod = row.Duzey1Kod;
                current.Duzey2Kod = row.Duzey2Kod;
                current.Duzey3Kod = row.Duzey3Kod;
                current.Duzey4Kod = row.Duzey4Kod;
                current.Duzey5Kod = row.Duzey5Kod;
                current.Ad = row.Ad;
                current.DuzeyNo = row.DuzeyNo;
                current.AktifMi = row.AktifMi;
                current.Aciklama = row.Aciklama;
                current.UstKodId = null;

                var dto = _mapper.Map<TasinirKodDto>(current);
                await base.UpdateAsync(dto);
                sonuc.Guncellenen++;
            }
            else
            {
                var dto = new TasinirKodDto
                {
                    TamKod = row.TamKod,
                    Duzey1Kod = row.Duzey1Kod,
                    Duzey2Kod = row.Duzey2Kod,
                    Duzey3Kod = row.Duzey3Kod,
                    Duzey4Kod = row.Duzey4Kod,
                    Duzey5Kod = row.Duzey5Kod,
                    Ad = row.Ad,
                    DuzeyNo = row.DuzeyNo,
                    AktifMi = row.AktifMi,
                    Aciklama = row.Aciklama
                };

                var created = await base.AddAsync(dto);
                var createdEntity = _mapper.Map<TasinirKod>(created);
                allByTamKod[row.TamKod] = createdEntity;
                sonuc.Eklenen++;
            }
        }

        var refreshed = await _repository.GetByTamKodlarAsync(normalizedRows.Select(x => x.TamKod), cancellationToken);
        var refreshedByTamKod = refreshed.ToDictionary(x => x.TamKod, StringComparer.OrdinalIgnoreCase);

        foreach (var row in normalizedRows.Where(x => !string.IsNullOrWhiteSpace(x.UstTamKod)))
        {
            if (!refreshedByTamKod.TryGetValue(row.TamKod, out var current) || string.IsNullOrWhiteSpace(row.UstTamKod))
            {
                continue;
            }

            if (!refreshedByTamKod.TryGetValue(row.UstTamKod, out var parent))
            {
                continue;
            }

            if (current.UstKodId == parent.Id)
            {
                continue;
            }

            var dto = _mapper.Map<TasinirKodDto>(current);
            dto.UstKodId = parent.Id;
            await base.UpdateAsync(dto);
            sonuc.Guncellenen++;
        }

        if (request.PasiflestirilmeyenleriPasifYap)
        {
            var importedSet = normalizedRows.Select(x => x.TamKod).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allActive = await _repository.GetAllAsync();
            foreach (var item in allActive.Where(x => x.AktifMi && !importedSet.Contains(x.TamKod)))
            {
                var dto = _mapper.Map<TasinirKodDto>(item);
                dto.AktifMi = false;
                await base.UpdateAsync(dto);
                sonuc.PasifYapilan++;
            }
        }

        InvalidateLookupCache();
        return sonuc;
    }

    private async Task<List<TasinirKodDto>> GetAllForLookupCachedAsync(CancellationToken cancellationToken)
    {
        var version = GetLookupCacheVersion();
        var cacheKey = $"{LookupCachePrefix}:All:v{version}";

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            var items = await _repository.GetAllAsync();
            return items
                .Select(x => _mapper.Map<TasinirKodDto>(x))
                .OrderBy(x => x.TamKod)
                .ThenBy(x => x.Id)
                .ToList();
        }) ?? [];
    }

    private static List<TasinirKodDto> ApplyLookupFilter(List<TasinirKodDto> source, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return source;
        }

        var isCodeQuery = normalizedQuery.All(c => char.IsDigit(c) || c == '.' || c == '*');
        if (isCodeQuery)
        {
            if (normalizedQuery.Contains('*'))
            {
                var wildcardPrefix = normalizedQuery.Split('*', 2, StringSplitOptions.None)[0];
                if (string.IsNullOrEmpty(wildcardPrefix))
                {
                    return source;
                }

                return source
                    .Where(x => x.TamKod.StartsWith(wildcardPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var prefix = $"{normalizedQuery}.";
            return source
                .Where(x => x.TamKod.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || x.TamKod.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return source
            .Where(x => x.TamKod.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || x.Ad.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private string BuildLookupCacheKey(int pageNumber, int pageSize, string query)
    {
        var version = GetLookupCacheVersion();
        var normalizedQuery = query.Trim().ToLowerInvariant();
        return $"{LookupCachePrefix}:Paged:v{version}:p{pageNumber}:s{pageSize}:q:{normalizedQuery}";
    }

    private int GetLookupCacheVersion()
    {
        return _memoryCache.GetOrCreate(CacheVersionKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 1;
        });
    }

    private void InvalidateLookupCache()
    {
        var nextVersion = GetLookupCacheVersion() + 1;
        _memoryCache.Set(CacheVersionKey, nextVersion, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }

    private async Task ValidateUniqueAsync(string tamKod, int? currentId)
    {
        var normalized = tamKod.Trim();
        var exists = await _repository.AnyAsync(x => x.TamKod == normalized && (!currentId.HasValue || x.Id != currentId.Value));
        if (exists)
        {
            throw new BaseException("Tam kod benzersiz olmalidir.", 400);
        }
    }

    private static void NormalizeAndValidate(TasinirKodDto dto)
    {
        dto.TamKod = dto.TamKod?.Trim() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Duzey1Kod = NormalizeOptional(dto.Duzey1Kod);
        dto.Duzey2Kod = NormalizeOptional(dto.Duzey2Kod);
        dto.Duzey3Kod = NormalizeOptional(dto.Duzey3Kod);
        dto.Duzey4Kod = NormalizeOptional(dto.Duzey4Kod);
        dto.Duzey5Kod = NormalizeOptional(dto.Duzey5Kod);
        dto.Aciklama = NormalizeOptional(dto.Aciklama);

        if (string.IsNullOrWhiteSpace(dto.TamKod))
        {
            throw new BaseException("Tam kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (dto.DuzeyNo <= 0)
        {
            throw new BaseException("Duzey no 0'dan buyuk olmalidir.", 400);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
