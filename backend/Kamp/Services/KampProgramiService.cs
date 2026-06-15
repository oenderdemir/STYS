using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampProgramiService : BaseRdbmsService<KampProgramiDto, KampProgrami, int>, IKampProgramiService
{
    private readonly IKampProgramiRepository _kampProgramiRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public KampProgramiService(
        IKampProgramiRepository kampProgramiRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IMapper mapper)
        : base(kampProgramiRepository, mapper)
    {
        _kampProgramiRepository = kampProgramiRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public override async Task<KampProgramiDto> AddAsync(KampProgramiDto dto)
    {
        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, null);

        var entity = Mapper.Map<KampProgrami>(dto);
        entity.KurumId = await ResolveCreateKurumIdAsync();

        await _kampProgramiRepository.AddAsync(entity);
        await _kampProgramiRepository.SaveChangesAsync();
        return Mapper.Map<KampProgramiDto>(entity);
    }

    public override async Task<KampProgramiDto> UpdateAsync(KampProgramiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kamp programi id zorunludur.", 400);
        }

        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, dto.Id.Value);

        var entity = await _kampProgramiRepository.GetByIdAsync(dto.Id.Value);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek kamp programi bulunamadi.", 404);
        }

        entity.IsDeleted = false;
        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.Aciklama = dto.Aciklama;
        entity.Yil = dto.Yil;
        entity.MaksimumBasvuruSayisi = dto.MaksimumBasvuruSayisi;
        entity.AktifMi = dto.AktifMi;

        _kampProgramiRepository.Update(entity);
        await _kampProgramiRepository.SaveChangesAsync();
        return Mapper.Map<KampProgramiDto>(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanManageGlobalAsync();

        var entity = await _kampProgramiRepository.GetByIdAsync(id);
        if (entity is null)
        {
            return;
        }

        var hasDonem = await _stysDbContext.KampDonemleri.AnyAsync(x => x.KampProgramiId == id);
        if (hasDonem)
        {
            throw new BaseException($"\"{entity.Ad}\" kamp programi altinda donem kaydi bulundugu icin silinemez.", 400);
        }

        await base.DeleteAsync(id);
    }

    private async Task EnsureCanManageGlobalAsync()
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            throw new BaseException("Kamp programi tanimlari yalnizca merkez yoneticileri tarafindan yonetilebilir.", 403);
        }
    }

    private async Task EnsureUniqueAsync(KampProgramiDto dto, int? excludedId)
    {
        var normalizedKod = dto.Kod.Trim().ToUpperInvariant();
        var normalizedAd = dto.Ad.Trim().ToUpperInvariant();

        var existsKod = await _stysDbContext.KampProgramlari.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId.Value)
            && x.Yil == dto.Yil
            && x.Kod.ToUpper() == normalizedKod);

        if (existsKod)
        {
            throw new BaseException($"{dto.Yil} yilinda ayni koda sahip baska bir kamp programi zaten mevcut.", 400);
        }

        var existsAd = await _stysDbContext.KampProgramlari.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId.Value)
            && x.Yil == dto.Yil
            && x.Ad.ToUpper() == normalizedAd);

        if (existsAd)
        {
            throw new BaseException($"{dto.Yil} yilinda ayni ada sahip baska bir kamp programi zaten mevcut.", 400);
        }
    }

    private async Task<int> ResolveCreateKurumIdAsync()
    {
        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (currentKurumId.HasValue)
        {
            var currentKurumExists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == currentKurumId.Value);
            if (currentKurumExists)
            {
                return currentKurumId.Value;
            }

            throw new BaseException("Aktif kurum bilgisi bulunamadi.", 400);
        }

        var defaultKurumId = await _stysDbContext.Kurumlar
            .Where(x => x.Kod == "DEFAULT" && x.AktifMi)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (!defaultKurumId.HasValue)
        {
            throw new BaseException("Varsayilan kurum bulunamadi.", 500);
        }

        return defaultKurumId.Value;
    }

    private static void Normalize(KampProgramiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kamp programi kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Kamp programi adi zorunludur.", 400);
        }

        if (dto.Yil < 2000 || dto.Yil > 2100)
        {
            throw new BaseException("Kamp programi yili 2000-2100 araliginda olmalidir.", 400);
        }

        if (dto.MaksimumBasvuruSayisi <= 0 || dto.MaksimumBasvuruSayisi > 20)
        {
            throw new BaseException("Kamp programi maksimum basvuru sayisi 1-20 araliginda olmalidir.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();
    }
}
