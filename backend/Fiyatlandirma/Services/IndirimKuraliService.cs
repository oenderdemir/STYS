using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using STYS.Fiyatlandirma.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri.Repositories;
using STYS.MisafirTipleri.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Fiyatlandirma.Services;

public class IndirimKuraliService : BaseRdbmsService<IndirimKuraliDto, IndirimKurali, int>, IIndirimKuraliService
{
    private readonly IIndirimKuraliRepository _indirimKuraliRepository;
    private readonly IMisafirTipiRepository _misafirTipiRepository;
    private readonly IKonaklamaTipiRepository _konaklamaTipiRepository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly StysAppDbContext _stysDbContext;

    public IndirimKuraliService(
        IIndirimKuraliRepository indirimKuraliRepository,
        IMisafirTipiRepository misafirTipiRepository,
        IKonaklamaTipiRepository konaklamaTipiRepository,
        ITesisRepository tesisRepository,
        IUserAccessScopeService userAccessScopeService,
        IHttpContextAccessor httpContextAccessor,
        StysAppDbContext stysAppDbContext,
        IMapper mapper)
        : base(indirimKuraliRepository, mapper)
    {
        _indirimKuraliRepository = indirimKuraliRepository;
        _misafirTipiRepository = misafirTipiRepository;
        _konaklamaTipiRepository = konaklamaTipiRepository;
        _tesisRepository = tesisRepository;
        _userAccessScopeService = userAccessScopeService;
        _httpContextAccessor = httpContextAccessor;
        _stysDbContext = stysAppDbContext;
    }

    public override async Task<IndirimKuraliDto> AddAsync(IndirimKuraliDto dto)
    {
        Normalize(dto);
        await EnsureManageScopeAsync(dto);
        await ValidateReferencesAsync(dto);

        var entity = Mapper.Map<IndirimKurali>(dto);
        entity.MisafirTipiKisitlari = dto.MisafirTipiIds
            .Distinct()
            .Select(x => new IndirimKuraliMisafirTipi { MisafirTipiId = x })
            .ToList();
        entity.KonaklamaTipiKisitlari = dto.KonaklamaTipiIds
            .Distinct()
            .Select(x => new IndirimKuraliKonaklamaTipi { KonaklamaTipiId = x })
            .ToList();

        await _indirimKuraliRepository.AddAsync(entity);
        await _indirimKuraliRepository.SaveChangesAsync();

        return await GetByIdInternalAsync(entity.Id) ?? throw new BaseException("Indirim kurali olusturulamadi.", 500);
    }

    public override async Task<IndirimKuraliDto> UpdateAsync(IndirimKuraliDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Indirim kurali id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureManageScopeAsync(dto);
        await ValidateReferencesAsync(dto);

        var entity = await _indirimKuraliRepository.GetByIdAsync(dto.Id.Value, IncludeAll);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek indirim kurali bulunamadi.", 404);
        }

        await EnsureCanManageExistingEntityAsync(entity);

        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.IndirimTipi = dto.IndirimTipi;
        entity.Deger = dto.Deger;
        entity.KapsamTipi = dto.KapsamTipi;
        entity.TesisId = dto.TesisId;
        entity.BaslangicTarihi = dto.BaslangicTarihi.Date;
        entity.BitisTarihi = dto.BitisTarihi.Date;
        entity.Oncelik = dto.Oncelik;
        entity.BirlesebilirMi = dto.BirlesebilirMi;
        entity.AktifMi = dto.AktifMi;

        SyncMisafirTipiKisitlari(entity, dto.MisafirTipiIds);
        SyncKonaklamaTipiKisitlari(entity, dto.KonaklamaTipiIds);

        _indirimKuraliRepository.Update(entity);
        await _indirimKuraliRepository.SaveChangesAsync();

        return await GetByIdInternalAsync(dto.Id.Value) ?? throw new BaseException("Indirim kurali guncellenemedi.", 500);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _indirimKuraliRepository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new BaseException("Silinecek indirim kurali bulunamadi.", 404);
        }

        await EnsureCanManageExistingEntityAsync(entity);
        await base.DeleteAsync(id);
    }

    public override async Task<IndirimKuraliDto?> GetByIdAsync(int id, Func<IQueryable<IndirimKurali>, IQueryable<IndirimKurali>>? include = null)
    {
        var entity = await _indirimKuraliRepository.GetByIdAsync(id, IncludeAll);
        if (entity is null)
        {
            return null;
        }

        await EnsureCanViewEntityAsync(entity);
        return Mapper.Map<IndirimKuraliDto>(entity);
    }

    public override async Task<IEnumerable<IndirimKuraliDto>> GetAllAsync(Func<IQueryable<IndirimKurali>, IQueryable<IndirimKurali>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var query = BuildScopedQuery(scope, _indirimKuraliRepository.Where(_ => true, IncludeAll));
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Ad).ToListAsync();
        return Mapper.Map<List<IndirimKuraliDto>>(items);
    }

    public override async Task<PagedResult<IndirimKuraliDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<IndirimKurali, bool>>? predicate = null,
        Func<IQueryable<IndirimKurali>, IQueryable<IndirimKurali>>? include = null,
        Func<IQueryable<IndirimKurali>, IOrderedQueryable<IndirimKurali>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var query = BuildScopedQuery(scope, _indirimKuraliRepository.Where(_ => true, IncludeAll));
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var (pageNumber, pageSize) = request.Normalize();
        var totalCount = await query.CountAsync();
        query = (orderBy is not null ? orderBy(query) : query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Ad));
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<IndirimKuraliDto>(Mapper.Map<List<IndirimKuraliDto>>(items), pageNumber, pageSize, totalCount);
    }

    private async Task<IndirimKuraliDto?> GetByIdInternalAsync(int id)
    {
        var entity = await _indirimKuraliRepository.GetByIdAsync(id, IncludeAll);
        return entity is null ? null : Mapper.Map<IndirimKuraliDto>(entity);
    }

    private static IQueryable<IndirimKurali> BuildScopedQuery(DomainAccessScope scope, IQueryable<IndirimKurali> query)
    {
        if (!scope.IsScoped)
        {
            return query;
        }

        return query.Where(x =>
            x.KapsamTipi == IndirimKapsamTipleri.Sistem ||
            (x.KapsamTipi == IndirimKapsamTipleri.Tesis && x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value)));
    }

    private async Task EnsureCanViewEntityAsync(IndirimKurali entity)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (entity.KapsamTipi.Equals(IndirimKapsamTipleri.Sistem, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!entity.TesisId.HasValue || !scope.TesisIds.Contains(entity.TesisId.Value))
        {
            throw new BaseException("Bu indirim kaydini goruntuleme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureCanManageExistingEntityAsync(IndirimKurali entity)
    {
        if (entity.KapsamTipi.Equals(IndirimKapsamTipleri.Sistem, StringComparison.OrdinalIgnoreCase))
        {
            EnsureCanManageSystemDiscountRules();
            return;
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!entity.TesisId.HasValue || !scope.TesisIds.Contains(entity.TesisId.Value))
        {
            throw new BaseException("Bu tesis altindaki indirim kurallarini yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureManageScopeAsync(IndirimKuraliDto dto)
    {
        if (dto.KapsamTipi.Equals(IndirimKapsamTipleri.Sistem, StringComparison.OrdinalIgnoreCase))
        {
            EnsureCanManageSystemDiscountRules();
            return;
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!dto.TesisId.HasValue || !scope.TesisIds.Contains(dto.TesisId.Value))
        {
            throw new BaseException("Sadece yonettiginiz tesisler icin indirim kurali tanimlayabilirsiniz.", 403);
        }
    }

    private void EnsureCanManageSystemDiscountRules()
    {
        if (HasPermission(StructurePermissions.IndirimKuraliYonetimi.SistemIndirimKuraliOlusturabilme))
        {
            return;
        }

        throw new BaseException("Sistem geneli indirim kurallari icin yetkiniz bulunmuyor.", 403);
    }

    private bool HasPermission(string permission)
    {
        var claims = _httpContextAccessor.HttpContext?.User
            .FindAll(TodPlatformAuthorizationConstants.PermissionClaimType)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        if (claims is null)
        {
            return false;
        }

        return claims.Any(x => x.Equals(permission, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ValidateReferencesAsync(IndirimKuraliDto dto)
    {
        if (dto.KapsamTipi.Equals(IndirimKapsamTipleri.Tesis, StringComparison.OrdinalIgnoreCase))
        {
            if (!dto.TesisId.HasValue || dto.TesisId.Value <= 0)
            {
                throw new BaseException("Tesis kapsamli indirim icin tesis secimi zorunludur.", 400);
            }

            var tesisExists = await _tesisRepository.AnyAsync(x => x.Id == dto.TesisId.Value && x.AktifMi);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi veya pasif.", 400);
            }
        }

        if (dto.MisafirTipiIds.Count > 0)
        {
            var existingMisafirTipiIds = await _misafirTipiRepository.Where(x => dto.MisafirTipiIds.Contains(x.Id) && x.AktifMi)
                .Select(x => x.Id)
                .ToListAsync();
            if (existingMisafirTipiIds.Count != dto.MisafirTipiIds.Distinct().Count())
            {
                throw new BaseException("Gecersiz veya pasif misafir tipi secildi.", 400);
            }

            if (dto.KapsamTipi.Equals(IndirimKapsamTipleri.Tesis, StringComparison.OrdinalIgnoreCase) && dto.TesisId.HasValue)
            {
                var tesisMisafirTipiIds = await _stysDbContext.TesisMisafirTipleri
                    .Where(x => x.TesisId == dto.TesisId.Value
                        && x.AktifMi
                        && !x.IsDeleted
                        && dto.MisafirTipiIds.Contains(x.MisafirTipiId))
                    .Select(x => x.MisafirTipiId)
                    .Distinct()
                    .ToListAsync();

                if (tesisMisafirTipiIds.Count != dto.MisafirTipiIds.Distinct().Count())
                {
                    throw new BaseException("Secilen misafir tiplerinden biri ilgili tesiste kullanima acik degil.", 400);
                }
            }
        }

        if (dto.KonaklamaTipiIds.Count > 0)
        {
            var existingKonaklamaTipiIds = await _konaklamaTipiRepository.Where(x => dto.KonaklamaTipiIds.Contains(x.Id) && x.AktifMi)
                .Select(x => x.Id)
                .ToListAsync();
            if (existingKonaklamaTipiIds.Count != dto.KonaklamaTipiIds.Distinct().Count())
            {
                throw new BaseException("Gecersiz veya pasif konaklama tipi secildi.", 400);
            }

            if (dto.KapsamTipi.Equals(IndirimKapsamTipleri.Tesis, StringComparison.OrdinalIgnoreCase) && dto.TesisId.HasValue)
            {
                var tesisKonaklamaTipiIds = await _stysDbContext.TesisKonaklamaTipleri
                    .Where(x => x.TesisId == dto.TesisId.Value
                        && x.AktifMi
                        && !x.IsDeleted
                        && dto.KonaklamaTipiIds.Contains(x.KonaklamaTipiId))
                    .Select(x => x.KonaklamaTipiId)
                    .Distinct()
                    .ToListAsync();

                if (tesisKonaklamaTipiIds.Count != dto.KonaklamaTipiIds.Distinct().Count())
                {
                    throw new BaseException("Secilen konaklama tiplerinden biri ilgili tesiste kullanima acik degil.", 400);
                }
            }
        }
    }

    private static void Normalize(IndirimKuraliDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Indirim kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Indirim adi zorunludur.", 400);
        }

        if (!IndirimTipleri.All.Contains(dto.IndirimTipi))
        {
            throw new BaseException("Gecersiz indirim tipi secildi.", 400);
        }

        if (!IndirimKapsamTipleri.All.Contains(dto.KapsamTipi))
        {
            throw new BaseException("Gecersiz kapsam tipi secildi.", 400);
        }

        if (dto.Deger <= 0)
        {
            throw new BaseException("Indirim degeri sifirdan buyuk olmalidir.", 400);
        }

        if (dto.BaslangicTarihi.Date > dto.BitisTarihi.Date)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.IndirimTipi = dto.IndirimTipi.Trim();
        dto.KapsamTipi = dto.KapsamTipi.Trim();
        dto.BaslangicTarihi = dto.BaslangicTarihi.Date;
        dto.BitisTarihi = dto.BitisTarihi.Date;

        dto.MisafirTipiIds = dto.MisafirTipiIds.Distinct().ToList();
        dto.KonaklamaTipiIds = dto.KonaklamaTipiIds.Distinct().ToList();

        if (dto.KapsamTipi.Equals(IndirimKapsamTipleri.Sistem, StringComparison.OrdinalIgnoreCase))
        {
            dto.TesisId = null;
        }
    }

    private static void SyncMisafirTipiKisitlari(IndirimKurali entity, IReadOnlyCollection<int> misafirTipiIds)
    {
        entity.MisafirTipiKisitlari ??= [];
        var targetIds = misafirTipiIds.ToHashSet();

        var toDelete = entity.MisafirTipiKisitlari.Where(x => !targetIds.Contains(x.MisafirTipiId)).ToList();
        foreach (var item in toDelete)
        {
            entity.MisafirTipiKisitlari.Remove(item);
        }

        var existingIds = entity.MisafirTipiKisitlari.Select(x => x.MisafirTipiId).ToHashSet();
        foreach (var targetId in targetIds)
        {
            if (existingIds.Contains(targetId))
            {
                continue;
            }

            entity.MisafirTipiKisitlari.Add(new IndirimKuraliMisafirTipi
            {
                MisafirTipiId = targetId
            });
        }
    }

    private static void SyncKonaklamaTipiKisitlari(IndirimKurali entity, IReadOnlyCollection<int> konaklamaTipiIds)
    {
        entity.KonaklamaTipiKisitlari ??= [];
        var targetIds = konaklamaTipiIds.ToHashSet();

        var toDelete = entity.KonaklamaTipiKisitlari.Where(x => !targetIds.Contains(x.KonaklamaTipiId)).ToList();
        foreach (var item in toDelete)
        {
            entity.KonaklamaTipiKisitlari.Remove(item);
        }

        var existingIds = entity.KonaklamaTipiKisitlari.Select(x => x.KonaklamaTipiId).ToHashSet();
        foreach (var targetId in targetIds)
        {
            if (existingIds.Contains(targetId))
            {
                continue;
            }

            entity.KonaklamaTipiKisitlari.Add(new IndirimKuraliKonaklamaTipi
            {
                KonaklamaTipiId = targetId
            });
        }
    }

    private static IQueryable<IndirimKurali> IncludeAll(IQueryable<IndirimKurali> query)
    {
        return query
            .Include(x => x.MisafirTipiKisitlari)
            .Include(x => x.KonaklamaTipiKisitlari);
    }
}
