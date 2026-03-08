using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaKullanimBloklari.Dto;
using STYS.OdaKullanimBloklari.Entities;
using STYS.OdaKullanimBloklari.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaKullanimBloklari.Services;

public class OdaKullanimBlokService : BaseRdbmsService<OdaKullanimBlokDto, OdaKullanimBlok, int>, IOdaKullanimBlokService
{
    private readonly IOdaKullanimBlokRepository _odaKullanimBlokRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public OdaKullanimBlokService(
        IOdaKullanimBlokRepository odaKullanimBlokRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(odaKullanimBlokRepository, mapper)
    {
        _odaKullanimBlokRepository = odaKullanimBlokRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public async Task<List<OdaKullanimBlokTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        var query = _stysDbContext.Tesisler
            .Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new OdaKullanimBlokTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OdaKullanimBlokOdaSecenekDto>> GetOdaSecenekleriAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        return await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
                where oda.AktifMi
                      && bina.AktifMi
                      && odaTipi.AktifMi
                      && bina.TesisId == tesisId
                select new OdaKullanimBlokOdaSecenekDto
                {
                    Id = oda.Id,
                    TesisId = bina.TesisId,
                    OdaNo = oda.OdaNo,
                    BinaAdi = bina.Ad,
                    OdaTipiAdi = odaTipi.Ad
                })
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public override async Task<OdaKullanimBlokDto> AddAsync(OdaKullanimBlokDto dto)
    {
        Normalize(dto);
        await ValidateTesisAndRoomAsync(dto.TesisId, dto.OdaId);
        await EnsureNoOverlapAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<OdaKullanimBlokDto> UpdateAsync(OdaKullanimBlokDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda kullanim blok id zorunludur.", 400);
        }

        var existing = await _odaKullanimBlokRepository.GetByIdAsync(dto.Id.Value);
        if (existing is null)
        {
            throw new BaseException("Guncellenecek oda kullanim blok kaydi bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existing.TesisId, CancellationToken.None);
        Normalize(dto);
        await ValidateTesisAndRoomAsync(dto.TesisId, dto.OdaId);
        await EnsureNoOverlapAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var existing = await _odaKullanimBlokRepository.GetByIdAsync(id);
        if (existing is null)
        {
            throw new BaseException("Silinecek oda kullanim blok kaydi bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existing.TesisId, CancellationToken.None);
        await base.DeleteAsync(id);
    }

    public override async Task<OdaKullanimBlokDto?> GetByIdAsync(int id, Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        return await base.GetByIdAsync(id, BuildScopedIncludeQuery(scope, include));
    }

    public override async Task<IEnumerable<OdaKullanimBlokDto>> GetAllAsync(Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        return await base.GetAllAsync(BuildScopedIncludeQuery(scope, include));
    }

    public override async Task<IEnumerable<OdaKullanimBlokDto>> WhereAsync(
        Expression<Func<OdaKullanimBlok, bool>> predicate,
        Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        return await base.WhereAsync(predicate, BuildScopedIncludeQuery(scope, include));
    }

    public override async Task<PagedResult<OdaKullanimBlokDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<OdaKullanimBlok, bool>>? predicate = null,
        Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>>? include = null,
        Func<IQueryable<OdaKullanimBlok>, IOrderedQueryable<OdaKullanimBlok>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        return await base.GetPagedAsync(request, predicate, BuildScopedIncludeQuery(scope, include), orderBy);
    }

    private async Task ValidateTesisAndRoomAsync(int tesisId, int odaId)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (odaId <= 0)
        {
            throw new BaseException("Oda secimi zorunludur.", 400);
        }

        await EnsureCanAccessTesisAsync(tesisId, CancellationToken.None);

        var tesisAktif = await _stysDbContext.Tesisler
            .AnyAsync(x => x.Id == tesisId && x.AktifMi);
        if (!tesisAktif)
        {
            throw new BaseException("Secilen tesis bulunamadi veya pasif.", 400);
        }

        var roomTesisId = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                where oda.Id == odaId
                      && oda.AktifMi
                      && bina.AktifMi
                select (int?)bina.TesisId)
            .FirstOrDefaultAsync();

        if (!roomTesisId.HasValue)
        {
            throw new BaseException("Secilen oda bulunamadi veya pasif.", 400);
        }

        if (roomTesisId.Value != tesisId)
        {
            throw new BaseException("Secilen oda, secilen tesise ait degil.", 400);
        }
    }

    private async Task EnsureNoOverlapAsync(OdaKullanimBlokDto dto, int? excludedId)
    {
        var overlapExists = await _odaKullanimBlokRepository.AnyAsync(x =>
            x.AktifMi
            && x.OdaId == dto.OdaId
            && x.BaslangicTarihi < dto.BitisTarihi
            && x.BitisTarihi > dto.BaslangicTarihi
            && (!excludedId.HasValue || x.Id != excludedId.Value));

        if (overlapExists)
        {
            throw new BaseException("Bu oda icin secilen tarih araliginda aktif bir bakim/ariza kaydi mevcut.", 400);
        }
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis icin oda bakim/ariza kaydi yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private static void Normalize(OdaKullanimBlokDto dto)
    {
        dto.BlokTipi = NormalizeBlokTipi(dto.BlokTipi);

        if (dto.BaslangicTarihi >= dto.BitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama)
            ? null
            : dto.Aciklama.Trim();
    }

    private static string NormalizeBlokTipi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BaseException("Blok tipi zorunludur.", 400);
        }

        if (value.Equals(OdaKullanimBlokTipleri.Bakim, StringComparison.OrdinalIgnoreCase))
        {
            return OdaKullanimBlokTipleri.Bakim;
        }

        if (value.Equals(OdaKullanimBlokTipleri.Ariza, StringComparison.OrdinalIgnoreCase))
        {
            return OdaKullanimBlokTipleri.Ariza;
        }

        throw new BaseException("Gecersiz blok tipi. Yalnizca Bakim veya Ariza olabilir.", 400);
    }

    private static Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<OdaKullanimBlok>, IQueryable<OdaKullanimBlok>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => scope.TesisIds.Contains(x.TesisId));
            }

            return result;
        };
    }
}

