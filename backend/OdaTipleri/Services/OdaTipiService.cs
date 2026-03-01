using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.OdaOzellikleri;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaOzellikleri.Repositories;
using STYS.OdaSiniflari.Repositories;
using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaTipleri.Services;

public class OdaTipiService : BaseRdbmsService<OdaTipiDto, OdaTipi, int>, IOdaTipiService
{
    private readonly IOdaTipiRepository _odaTipiRepository;
    private readonly ITesisOdaTipiOzellikDegerRepository _tesisOdaTipiOzellikDegerRepository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IOdaSinifiRepository _odaSinifiRepository;
    private readonly IOdaOzellikRepository _odaOzellikRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public OdaTipiService(
        IOdaTipiRepository odaTipiRepository,
        ITesisOdaTipiOzellikDegerRepository tesisOdaTipiOzellikDegerRepository,
        ITesisRepository tesisRepository,
        IOdaSinifiRepository odaSinifiRepository,
        IOdaOzellikRepository odaOzellikRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(odaTipiRepository, mapper)
    {
        _odaTipiRepository = odaTipiRepository;
        _tesisOdaTipiOzellikDegerRepository = tesisOdaTipiOzellikDegerRepository;
        _tesisRepository = tesisRepository;
        _odaSinifiRepository = odaSinifiRepository;
        _odaOzellikRepository = odaOzellikRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<OdaTipiDto> AddAsync(OdaTipiDto dto)
    {
        Normalize(dto);
        await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveNameAsync(dto, null);
        var normalizedFeatureValues = await NormalizeAndValidateFeatureValuesAsync(dto.OdaOzellikDegerleri);

        var entity = Mapper.Map<OdaTipi>(dto);
        entity.OdaOzellikDegerleri = normalizedFeatureValues
            .Select(x => new TesisOdaTipiOzellikDeger
            {
                OdaOzellikId = x.OdaOzellikId,
                Deger = x.Deger
            })
            .ToList();

        await _odaTipiRepository.AddAsync(entity);
        await _odaTipiRepository.SaveChangesAsync();

        return Mapper.Map<OdaTipiDto>(entity);
    }

    public override async Task<OdaTipiDto> UpdateAsync(OdaTipiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda tipi id zorunludur.", 400);
        }

        var existingEntity = await _odaTipiRepository.GetByIdAsync(dto.Id.Value, query => query.Include(x => x.OdaOzellikDegerleri));
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek oda tipi bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existingEntity.TesisId);
        Normalize(dto);
        await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        var normalizedFeatureValues = await NormalizeAndValidateFeatureValuesAsync(dto.OdaOzellikDegerleri);

        existingEntity.IsDeleted = false;
        existingEntity.TesisId = dto.TesisId;
        existingEntity.OdaSinifiId = dto.OdaSinifiId;
        existingEntity.Ad = dto.Ad;
        existingEntity.PaylasimliMi = dto.PaylasimliMi;
        existingEntity.Kapasite = dto.Kapasite;
        existingEntity.AktifMi = dto.AktifMi;

        SyncFeatureValues(existingEntity, normalizedFeatureValues);

        _odaTipiRepository.Update(existingEntity);
        await _odaTipiRepository.SaveChangesAsync();

        return Mapper.Map<OdaTipiDto>(existingEntity);
    }

    public override async Task DeleteAsync(int id)
    {
        var existingEntity = await _odaTipiRepository.GetByIdAsync(id);
        if (existingEntity is null)
        {
            throw new BaseException("Silinecek oda tipi bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existingEntity.TesisId);
        await base.DeleteAsync(id);
    }

    private async Task EnsureDependenciesAsync(OdaTipiDto dto)
    {
        var tesis = await _tesisRepository.GetByIdAsync(dto.TesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 400);
        }

        if (!tesis.AktifMi)
        {
            throw new BaseException("Pasif tesis altinda oda tipi olusturulamaz veya guncellenemez.", 400);
        }

        await EnsureCanAccessTesisAsync(dto.TesisId);

        var odaSinifi = await _odaSinifiRepository.GetByIdAsync(dto.OdaSinifiId);
        if (odaSinifi is null)
        {
            throw new BaseException("Secilen oda sinifi bulunamadi.", 400);
        }

        if (!odaSinifi.AktifMi)
        {
            throw new BaseException("Pasif oda sinifi secilemez.", 400);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(OdaTipiDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _odaTipiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.TesisId == dto.TesisId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni isimde aktif oda tipi zaten mevcut.", 400);
        }
    }

    private static void Normalize(OdaTipiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Oda tipi adi zorunludur.", 400);
        }

        if (dto.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (dto.OdaSinifiId <= 0)
        {
            throw new BaseException("Oda sinifi secimi zorunludur.", 400);
        }

        if (dto.Kapasite <= 0)
        {
            throw new BaseException("Kapasite sifirdan buyuk olmalidir.", 400);
        }

        dto.Ad = dto.Ad.Trim();
        dto.OdaOzellikDegerleri ??= [];
    }

    private async Task<List<TesisOdaTipiOzellikDegerNormalized>> NormalizeAndValidateFeatureValuesAsync(ICollection<TesisOdaTipiOzellikDegerDto>? featureValues)
    {
        if (featureValues is null || featureValues.Count == 0)
        {
            return [];
        }

        var duplicateFeatureId = featureValues
            .GroupBy(x => x.OdaOzellikId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (duplicateFeatureId > 0)
        {
            throw new BaseException("Ayni ozellik bir oda tipinde bir kez secilebilir.", 400);
        }

        var odaOzellikleri = (await _odaOzellikRepository.GetAllAsync()).ToDictionary(x => x.Id);
        var normalizedValues = new List<TesisOdaTipiOzellikDegerNormalized>();

        foreach (var featureValue in featureValues)
        {
            if (featureValue.OdaOzellikId <= 0)
            {
                throw new BaseException("Oda ozellik secimi gecersiz.", 400);
            }

            if (!odaOzellikleri.TryGetValue(featureValue.OdaOzellikId, out var odaOzellik))
            {
                throw new BaseException("Secilen oda ozelligi bulunamadi.", 400);
            }

            var normalizedValue = NormalizeFeatureValue(odaOzellik, featureValue.Deger);
            if (normalizedValue is null)
            {
                continue;
            }

            normalizedValues.Add(new TesisOdaTipiOzellikDegerNormalized(featureValue.OdaOzellikId, normalizedValue));
        }

        return normalizedValues;
    }

    private static string? NormalizeFeatureValue(OdaOzellik odaOzellik, string? rawValue)
    {
        var trimmed = rawValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (odaOzellik.VeriTipi == OdaOzellikVeriTipleri.Boolean)
        {
            if (trimmed == "1")
            {
                return bool.TrueString.ToLowerInvariant();
            }

            if (trimmed == "0")
            {
                return bool.FalseString.ToLowerInvariant();
            }

            if (bool.TryParse(trimmed, out var boolValue))
            {
                return boolValue ? bool.TrueString.ToLowerInvariant() : bool.FalseString.ToLowerInvariant();
            }

            throw new BaseException($"'{odaOzellik.Ad}' icin gecersiz boolean deger.", 400);
        }

        if (odaOzellik.VeriTipi == OdaOzellikVeriTipleri.Number)
        {
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
            {
                return invariantValue.ToString(CultureInfo.InvariantCulture);
            }

            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var trValue))
            {
                return trValue.ToString(CultureInfo.InvariantCulture);
            }

            throw new BaseException($"'{odaOzellik.Ad}' icin gecersiz sayisal deger.", 400);
        }

        if (trimmed.Length > 512)
        {
            throw new BaseException($"'{odaOzellik.Ad}' icin girilen deger 512 karakteri asamaz.", 400);
        }

        return trimmed;
    }

    private void SyncFeatureValues(OdaTipi entity, IReadOnlyCollection<TesisOdaTipiOzellikDegerNormalized> normalizedValues)
    {
        entity.OdaOzellikDegerleri ??= [];

        var byFeatureId = entity.OdaOzellikDegerleri.ToDictionary(x => x.OdaOzellikId);
        var desiredFeatureIds = normalizedValues.Select(x => x.OdaOzellikId).ToHashSet();

        var valuesToDelete = entity.OdaOzellikDegerleri
            .Where(x => !desiredFeatureIds.Contains(x.OdaOzellikId))
            .ToList();

        if (valuesToDelete.Count > 0)
        {
            _tesisOdaTipiOzellikDegerRepository.DeleteRange(valuesToDelete);
        }

        foreach (var normalizedValue in normalizedValues)
        {
            if (byFeatureId.TryGetValue(normalizedValue.OdaOzellikId, out var existingValue))
            {
                existingValue.Deger = normalizedValue.Deger;
                continue;
            }

            entity.OdaOzellikDegerleri.Add(new TesisOdaTipiOzellikDeger
            {
                OdaOzellikId = normalizedValue.OdaOzellikId,
                Deger = normalizedValue.Deger
            });
        }
    }

    private sealed record TesisOdaTipiOzellikDegerNormalized(int OdaOzellikId, string Deger);

    public override async Task<OdaTipiDto?> GetByIdAsync(int id, Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<OdaTipiDto>> GetAllAsync(Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<OdaTipiDto>> WhereAsync(Expression<Func<OdaTipi, bool>> predicate, Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<OdaTipiDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<OdaTipi, bool>>? predicate = null,
        Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>>? include = null,
        Func<IQueryable<OdaTipi>, IOrderedQueryable<OdaTipi>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<OdaTipi>, IQueryable<OdaTipi>>? include)
    {
        return query =>
        {
            var result = include ?? (x => x.Include(y => y.OdaOzellikDegerleri));
            var scopedQuery = result(query);
            if (scope.IsScoped)
            {
                scopedQuery = scopedQuery.Where(x => scope.TesisIds.Contains(x.TesisId));
            }

            return scopedQuery;
        };
    }
}
