using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Binalar.Repositories;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using STYS.Odalar.Repositories;
using STYS.OdaOzellikleri;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaOzellikleri.Repositories;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Odalar.Services;

public class OdaService : BaseRdbmsService<OdaDto, Oda, int>, IOdaService
{
    private const string YatakSayisiOzellikKodu = "YATAK_SAYISI";

    private readonly IOdaRepository _odaRepository;
    private readonly IBinaRepository _binaRepository;
    private readonly IOdaTipiRepository _odaTipiRepository;
    private readonly IOdaOzellikRepository _odaOzellikRepository;
    private readonly IOdaOzellikDegerRepository _odaOzellikDegerRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public OdaService(
        IOdaRepository odaRepository,
        IBinaRepository binaRepository,
        IOdaTipiRepository odaTipiRepository,
        IOdaOzellikRepository odaOzellikRepository,
        IOdaOzellikDegerRepository odaOzellikDegerRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(odaRepository, mapper)
    {
        _odaRepository = odaRepository;
        _binaRepository = binaRepository;
        _odaTipiRepository = odaTipiRepository;
        _odaOzellikRepository = odaOzellikRepository;
        _odaOzellikDegerRepository = odaOzellikDegerRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<OdaDto> AddAsync(OdaDto dto)
    {
        Normalize(dto);
        var odaTipi = await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveRoomNoAsync(dto, null);
        var normalizedOdaOzellikDegerleri = await NormalizeAndValidateOdaOzellikDegerleriAsync(dto.OdaOzellikDegerleri);
        var defaultFeatureValues = GetDefaultFeatureValuesFromOdaTipi(odaTipi);
        var finalFeatureValues = MergeDefaultAndInputFeatureValues(defaultFeatureValues, normalizedOdaOzellikDegerleri);
        await ValidateBedCountAsync(finalFeatureValues, odaTipi.Kapasite, odaTipi.PaylasimliMi);

        var entity = Mapper.Map<Oda>(dto);
        entity.OdaOzellikDegerleri = finalFeatureValues
            .Select(x => new OdaOzellikDeger
            {
                OdaOzellikId = x.OdaOzellikId,
                Deger = x.Deger
            })
            .ToList();

        await _odaRepository.AddAsync(entity);
        await _odaRepository.SaveChangesAsync();

        return Mapper.Map<OdaDto>(entity);
    }

    public override async Task<OdaDto> UpdateAsync(OdaDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda id zorunludur.", 400);
        }

        var existingEntity = await _odaRepository.GetByIdAsync(dto.Id.Value, query => query.Include(x => x.OdaOzellikDegerleri));
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek oda bulunamadi.", 404);
        }

        await EnsureCanAccessBinaAsync(existingEntity.BinaId);
        Normalize(dto);
        var odaTipi = await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveRoomNoAsync(dto, dto.Id.Value);
        var normalizedOdaOzellikDegerleri = await NormalizeAndValidateOdaOzellikDegerleriAsync(dto.OdaOzellikDegerleri);
        await ValidateBedCountAsync(normalizedOdaOzellikDegerleri, odaTipi.Kapasite, odaTipi.PaylasimliMi);

        existingEntity.IsDeleted = false;
        existingEntity.OdaNo = dto.OdaNo;
        existingEntity.BinaId = dto.BinaId;
        existingEntity.TesisOdaTipiId = dto.TesisOdaTipiId;
        existingEntity.KatNo = dto.KatNo;
        existingEntity.AktifMi = dto.AktifMi;

        SyncOdaOzellikDegerleri(existingEntity, normalizedOdaOzellikDegerleri);

        _odaRepository.Update(existingEntity);
        await _odaRepository.SaveChangesAsync();

        return Mapper.Map<OdaDto>(existingEntity);
    }

    public override async Task DeleteAsync(int id)
    {
        var existingEntity = await _odaRepository.GetByIdAsync(id);
        if (existingEntity is null)
        {
            throw new BaseException("Silinecek oda bulunamadi.", 404);
        }

        await EnsureCanAccessBinaAsync(existingEntity.BinaId);
        await base.DeleteAsync(id);
    }

    private async Task<OdaTipi> EnsureDependenciesAsync(OdaDto dto)
    {
        var bina = await _binaRepository.GetByIdAsync(dto.BinaId);
        if (bina is null)
        {
            throw new BaseException("Secilen bina bulunamadi.", 400);
        }

        if (!bina.AktifMi)
        {
            throw new BaseException("Pasif bina altinda oda olusturulamaz veya guncellenemez.", 400);
        }

        await EnsureCanAccessBinaAsync(bina.Id);

        var odaTipi = await _odaTipiRepository.GetByIdAsync(dto.TesisOdaTipiId, query => query.Include(x => x.OdaOzellikDegerleri));
        if (odaTipi is null)
        {
            throw new BaseException("Secilen tesis oda tipi bulunamadi.", 400);
        }

        if (!odaTipi.AktifMi)
        {
            throw new BaseException("Pasif tesis oda tipi secilemez.", 400);
        }

        if (odaTipi.TesisId != bina.TesisId)
        {
            throw new BaseException("Secilen oda tipi, odanin bulundugu tesis ile uyumlu degil.", 400);
        }

        return odaTipi;
    }

    private async Task EnsureUniqueActiveRoomNoAsync(OdaDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedRoomNo = dto.OdaNo.Trim().ToUpperInvariant();
        var exists = await _odaRepository.AnyAsync(x =>
            x.AktifMi &&
            x.BinaId == dto.BinaId &&
            x.OdaNo.ToUpper() == normalizedRoomNo &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni bina altinda ayni oda numarasina sahip aktif oda zaten mevcut.", 400);
        }
    }

    private async Task ValidateBedCountAsync(
        IReadOnlyCollection<OdaOzellikDegerNormalized> odaOzellikDegerleri,
        int odaTipiKapasitesi,
        bool paylasimliMi)
    {
        var yatakSayisi = await GetBedCountFromDynamicFeaturesAsync(odaOzellikDegerleri);

        if (paylasimliMi)
        {
            if (!yatakSayisi.HasValue || yatakSayisi.Value <= 0)
            {
                throw new BaseException("Paylasimli oda icin yatak sayisi zorunludur.", 400);
            }

            if (yatakSayisi.Value > odaTipiKapasitesi)
            {
                throw new BaseException("Yatak sayisi oda tipi kapasitesini asamaz.", 400);
            }

            return;
        }

        if (yatakSayisi.HasValue && yatakSayisi.Value <= 0)
        {
            throw new BaseException("Yatak sayisi girilecekse sifirdan buyuk olmalidir.", 400);
        }
    }

    private async Task<int?> GetBedCountFromDynamicFeaturesAsync(IReadOnlyCollection<OdaOzellikDegerNormalized> odaOzellikDegerleri)
    {
        var yatakSayisiOzellik = (await _odaOzellikRepository.GetAllAsync())
            .FirstOrDefault(x => string.Equals(x.Kod, YatakSayisiOzellikKodu, StringComparison.OrdinalIgnoreCase));

        if (yatakSayisiOzellik is null)
        {
            return null;
        }

        var yatakSayisiDegeri = odaOzellikDegerleri
            .FirstOrDefault(x => x.OdaOzellikId == yatakSayisiOzellik.Id)?
            .Deger;

        if (string.IsNullOrWhiteSpace(yatakSayisiDegeri))
        {
            return null;
        }

        if (!decimal.TryParse(yatakSayisiDegeri, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new BaseException("Yatak sayisi ozelligi icin gecersiz sayisal deger.", 400);
        }

        if (parsedValue % 1 != 0)
        {
            throw new BaseException("Yatak sayisi tam sayi olmalidir.", 400);
        }

        if (parsedValue <= 0)
        {
            throw new BaseException("Yatak sayisi sifirdan buyuk olmalidir.", 400);
        }

        return Convert.ToInt32(parsedValue);
    }

    private static void Normalize(OdaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OdaNo))
        {
            throw new BaseException("Oda numarasi zorunludur.", 400);
        }

        if (dto.BinaId <= 0)
        {
            throw new BaseException("Bina secimi zorunludur.", 400);
        }

        if (dto.TesisOdaTipiId <= 0)
        {
            throw new BaseException("Tesis oda tipi secimi zorunludur.", 400);
        }

        dto.OdaNo = dto.OdaNo.Trim();
        dto.OdaOzellikDegerleri ??= [];
    }

    private async Task<List<OdaOzellikDegerNormalized>> NormalizeAndValidateOdaOzellikDegerleriAsync(ICollection<OdaOzellikDegerDto>? odaOzellikDegerleri)
    {
        if (odaOzellikDegerleri is null || odaOzellikDegerleri.Count == 0)
        {
            return [];
        }

        var duplicateFeatureId = odaOzellikDegerleri
            .GroupBy(x => x.OdaOzellikId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (duplicateFeatureId > 0)
        {
            throw new BaseException("Ayni oda ozelligi bir odada bir kez secilebilir.", 400);
        }

        var odaOzellikleri = (await _odaOzellikRepository.GetAllAsync())
            .ToDictionary(x => x.Id);

        var normalizedValues = new List<OdaOzellikDegerNormalized>();

        foreach (var odaOzellikDegeri in odaOzellikDegerleri)
        {
            if (odaOzellikDegeri.OdaOzellikId <= 0)
            {
                throw new BaseException("Oda ozellik secimi gecersiz.", 400);
            }

            if (!odaOzellikleri.TryGetValue(odaOzellikDegeri.OdaOzellikId, out var odaOzellik))
            {
                throw new BaseException("Secilen oda ozelligi bulunamadi.", 400);
            }

            var normalizedValue = NormalizeFeatureValue(odaOzellik, odaOzellikDegeri.Deger);
            if (normalizedValue is null)
            {
                continue;
            }

            normalizedValues.Add(new OdaOzellikDegerNormalized(odaOzellikDegeri.OdaOzellikId, normalizedValue));
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

    private void SyncOdaOzellikDegerleri(Oda entity, IReadOnlyCollection<OdaOzellikDegerNormalized> normalizedValues)
    {
        entity.OdaOzellikDegerleri ??= [];

        var byFeatureId = entity.OdaOzellikDegerleri.ToDictionary(x => x.OdaOzellikId);
        var desiredFeatureIds = normalizedValues.Select(x => x.OdaOzellikId).ToHashSet();

        var valuesToDelete = entity.OdaOzellikDegerleri
            .Where(x => !desiredFeatureIds.Contains(x.OdaOzellikId))
            .ToList();

        if (valuesToDelete.Count > 0)
        {
            _odaOzellikDegerRepository.DeleteRange(valuesToDelete);
        }

        foreach (var normalizedValue in normalizedValues)
        {
            if (byFeatureId.TryGetValue(normalizedValue.OdaOzellikId, out var existingValue))
            {
                existingValue.Deger = normalizedValue.Deger;
                continue;
            }

            entity.OdaOzellikDegerleri.Add(new OdaOzellikDeger
            {
                OdaOzellikId = normalizedValue.OdaOzellikId,
                Deger = normalizedValue.Deger
            });
        }
    }

    private sealed record OdaOzellikDegerNormalized(int OdaOzellikId, string Deger);

    private static List<OdaOzellikDegerNormalized> GetDefaultFeatureValuesFromOdaTipi(OdaTipi odaTipi)
    {
        if (odaTipi.OdaOzellikDegerleri is null || odaTipi.OdaOzellikDegerleri.Count == 0)
        {
            return [];
        }

        return odaTipi.OdaOzellikDegerleri
            .Where(x => !string.IsNullOrWhiteSpace(x.Deger))
            .Select(x => new OdaOzellikDegerNormalized(x.OdaOzellikId, x.Deger!.Trim()))
            .ToList();
    }

    private static List<OdaOzellikDegerNormalized> MergeDefaultAndInputFeatureValues(
        IReadOnlyCollection<OdaOzellikDegerNormalized> defaultValues,
        IReadOnlyCollection<OdaOzellikDegerNormalized> inputValues)
    {
        var merged = defaultValues.ToDictionary(x => x.OdaOzellikId, x => x.Deger);

        foreach (var inputValue in inputValues)
        {
            merged[inputValue.OdaOzellikId] = inputValue.Deger;
        }

        return merged.Select(x => new OdaOzellikDegerNormalized(x.Key, x.Value)).ToList();
    }

    public override async Task<OdaDto?> GetByIdAsync(int id, Func<IQueryable<Oda>, IQueryable<Oda>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<OdaDto>> GetAllAsync(Func<IQueryable<Oda>, IQueryable<Oda>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<OdaDto>> WhereAsync(Expression<Func<Oda, bool>> predicate, Func<IQueryable<Oda>, IQueryable<Oda>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<OdaDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<Oda, bool>>? predicate = null,
        Func<IQueryable<Oda>, IQueryable<Oda>>? include = null,
        Func<IQueryable<Oda>, IOrderedQueryable<Oda>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureCanAccessBinaAsync(int binaId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.BinaIds.Contains(binaId))
        {
            throw new BaseException("Bu bina altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<Oda>, IQueryable<Oda>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Oda>, IQueryable<Oda>>? include)
    {
        return query =>
        {
            var result = include ?? (x => x.Include(y => y.OdaOzellikDegerleri));
            var scopedQuery = result(query);
            if (scope.IsScoped)
            {
                scopedQuery = scopedQuery.Where(x => scope.BinaIds.Contains(x.BinaId));
            }

            return scopedQuery;
        };
    }
}
