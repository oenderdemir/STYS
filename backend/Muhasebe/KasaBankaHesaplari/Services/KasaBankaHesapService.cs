using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KasaBankaHesaplari.Services;

public class KasaBankaHesapService : BaseRdbmsService<KasaBankaHesapDto, KasaBankaHesap, int>, IKasaBankaHesapService
{
    private readonly IKasaBankaHesapRepository _repository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDetayHesapService _muhasebeDetayHesapService;

    public KasaBankaHesapService(
        IKasaBankaHesapRepository repository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext dbContext,
        IMuhasebeDetayHesapService muhasebeDetayHesapService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _userAccessScopeService = userAccessScopeService;
        _dbContext = dbContext;
        _muhasebeDetayHesapService = muhasebeDetayHesapService;
    }

    public override async Task<KasaBankaHesapDto> AddAsync(KasaBankaHesapDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        NormalizeBasicFields(dto);

        if (!dto.TesisId.HasValue || dto.TesisId.Value <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        var anaHesapKodu = ResolveAnaHesapKodu(dto.Tip);
        await ApplyTipDefaultsAndValidateAsync(dto);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
            var muhasebeDetay = await _muhasebeDetayHesapService.CreateOrResolveDetayHesapAsync(
                dto.TesisId.Value,
                anaHesapKodu,
                "FinansalHesap",
                dto.Ad,
                null,
                CancellationToken.None);

            var entity = new KasaBankaHesap
            {
                TesisId = dto.TesisId,
                Tip = dto.Tip,
                Kod = muhasebeDetay.Kod,
                Ad = dto.Ad,
                MuhasebeHesapPlaniId = muhasebeDetay.MuhasebeHesapPlaniId,
                AnaMuhasebeHesapKodu = muhasebeDetay.AnaMuhasebeHesapKodu,
                MuhasebeHesapSiraNo = muhasebeDetay.SiraNo,
                ParaBirimi = dto.ParaBirimi,
                ValorGunSayisi = dto.ValorGunSayisi,
                KartAdi = dto.KartAdi,
                KartNoMaskeli = dto.KartNoMaskeli,
                KartLimiti = dto.KartLimiti,
                HesapKesimGunu = dto.HesapKesimGunu,
                SonOdemeGunu = dto.SonOdemeGunu,
                BagliBankaHesapId = dto.BagliBankaHesapId,
                BankaAdi = dto.BankaAdi,
                SubeAdi = dto.SubeAdi,
                HesapNo = dto.HesapNo,
                Iban = dto.Iban,
                MusteriNo = dto.MusteriNo,
                HesapTuru = dto.HesapTuru,
                SorumluKisi = dto.SorumluKisi,
                Lokasyon = dto.Lokasyon,
                AktifMi = dto.AktifMi,
                Aciklama = dto.Aciklama
            };

            await _dbContext.KasaBankaHesaplari.AddAsync(entity, CancellationToken.None);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);
            return Mapper.Map<KasaBankaHesapDto>(entity);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task<KasaBankaHesapDto> UpdateAsync(KasaBankaHesapDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap id zorunludur.", 400);
        }

        NormalizeBasicFields(dto);
        var entity = await _dbContext.KasaBankaHesaplari.FirstOrDefaultAsync(x => x.Id == dto.Id.Value)
            ?? throw new BaseException("Finansal hesap bulunamadi.", 404);

        await EnsureCanAccessTesisAsync(entity.TesisId, CancellationToken.None);
        var nextTesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id.Value);
        var hasMuhasebeLink = entity.MuhasebeHesapPlaniId.HasValue;

        if (hasMuhasebeLink && !string.Equals(entity.Tip, dto.Tip, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş finansal hesaplarda tip değiştirilemez.", 400);
        }

        if (hasMuhasebeLink && entity.TesisId != nextTesisId)
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş finansal hesaplarda tesis değiştirilemez.", 400);
        }

        await ApplyTipDefaultsAndValidateAsync(dto);

        entity.TesisId = nextTesisId;
        entity.Ad = dto.Ad;
        entity.ParaBirimi = dto.ParaBirimi;
        entity.ValorGunSayisi = dto.ValorGunSayisi;
        entity.KartAdi = dto.KartAdi;
        entity.KartNoMaskeli = dto.KartNoMaskeli;
        entity.KartLimiti = dto.KartLimiti;
        entity.HesapKesimGunu = dto.HesapKesimGunu;
        entity.SonOdemeGunu = dto.SonOdemeGunu;
        entity.BagliBankaHesapId = dto.BagliBankaHesapId;
        entity.BankaAdi = dto.BankaAdi;
        entity.SubeAdi = dto.SubeAdi;
        entity.HesapNo = dto.HesapNo;
        entity.Iban = dto.Iban;
        entity.MusteriNo = dto.MusteriNo;
        entity.HesapTuru = dto.HesapTuru;
        entity.SorumluKisi = dto.SorumluKisi;
        entity.Lokasyon = dto.Lokasyon;
        entity.AktifMi = dto.AktifMi;
        entity.Aciklama = dto.Aciklama;

        if (entity.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == entity.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.Ad = entity.Ad;
                hesap.TesisId = entity.TesisId;
                if (!entity.AktifMi)
                {
                    hesap.AktifMi = false;
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        return Mapper.Map<KasaBankaHesapDto>(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _dbContext.KasaBankaHesaplari.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            throw new BaseException("Finansal hesap bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(entity.TesisId, CancellationToken.None);
        await base.DeleteAsync(id);

        if (entity.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == entity.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.AktifMi = false;
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task<List<KasaBankaHesapDto>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var items = await _repository.GetByTipAsync(tip, onlyActive, cancellationToken);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            items = items.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value)).ToList();
        }

        return items.Select(Mapper.Map<KasaBankaHesapDto>).ToList();
    }

    public async Task<List<MuhasebeHesapSecimDto>> GetMuhasebeHesapSecimleriAsync(string tip, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var prefix = ResolveAnaHesapKodu(tip);
        var matches = await _dbContext.MuhasebeHesapPlanlari
            .Where(x => !x.IsDeleted && x.AktifMi && x.TesisId == null && (x.Kod == prefix || x.TamKod.StartsWith(prefix)))
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return matches.Select(x => new MuhasebeHesapSecimDto
        {
            Id = x.Id,
            TamKod = x.TamKod,
            Ad = x.Ad
        }).ToList();
    }

    public override async Task<KasaBankaHesapDto?> GetByIdAsync(int id, Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<KasaBankaHesapDto>> GetAllAsync(Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<KasaBankaHesapDto>> WhereAsync(System.Linq.Expressions.Expression<Func<KasaBankaHesap, bool>> predicate, Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<KasaBankaHesapDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<KasaBankaHesap, bool>>? predicate = null,
        Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null,
        Func<IQueryable<KasaBankaHesap>, IOrderedQueryable<KasaBankaHesap>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private static string ResolveAnaHesapKodu(string tip)
    {
        return tip switch
        {
            KasaBankaHesapTipleri.NakitKasa => MuhasebeAnaHesapKodlari.FinansalKasa,
            KasaBankaHesapTipleri.Banka => MuhasebeAnaHesapKodlari.FinansalBanka,
            KasaBankaHesapTipleri.DovizHesabi => MuhasebeAnaHesapKodlari.FinansalBanka,
            KasaBankaHesapTipleri.KrediKarti => MuhasebeAnaHesapKodlari.FinansalKrediKarti,
            _ => throw new BaseException("Hesap tipi gecersiz.", 400)
        };
    }

    private static int ResolveDefaultValorGunSayisi(string tip)
    {
        return tip == KasaBankaHesapTipleri.KrediKarti ? 1 : 0;
    }

    private async Task ApplyTipDefaultsAndValidateAsync(KasaBankaHesapDto dto)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(dto.Tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        dto.ParaBirimi = NormalizeOptional(dto.ParaBirimi?.ToUpperInvariant(), 3) ?? "TRY";
        dto.BankaAdi = NormalizeOptional(dto.BankaAdi, 128);
        dto.SubeAdi = NormalizeOptional(dto.SubeAdi, 128);
        dto.HesapNo = NormalizeOptional(dto.HesapNo, 64);
        dto.Iban = NormalizeOptional(dto.Iban?.Replace(" ", string.Empty).ToUpperInvariant(), 34);
        dto.MusteriNo = NormalizeOptional(dto.MusteriNo, 64);
        dto.HesapTuru = NormalizeOptional(dto.HesapTuru, 32);
        dto.KartAdi = NormalizeOptional(dto.KartAdi, 128);
        dto.KartNoMaskeli = NormalizeOptional(dto.KartNoMaskeli, 32);
        dto.SorumluKisi = NormalizeOptional(dto.SorumluKisi, 128);
        dto.Lokasyon = NormalizeOptional(dto.Lokasyon, 128);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        if (dto.ValorGunSayisi == 0 && dto.Id is null)
        {
            dto.ValorGunSayisi = ResolveDefaultValorGunSayisi(dto.Tip);
        }
        if (dto.ValorGunSayisi < 0 || dto.ValorGunSayisi > 365)
        {
            throw new BaseException("Valör süresi 0 ile 365 arasında olmalıdır.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.DovizHesabi && string.IsNullOrWhiteSpace(dto.ParaBirimi))
        {
            throw new BaseException("Doviz hesabi icin para birimi zorunludur.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.Banka || dto.Tip == KasaBankaHesapTipleri.DovizHesabi)
        {
            if (string.IsNullOrWhiteSpace(dto.BankaAdi))
            {
                throw new BaseException("Banka/Doviz tipi hesap icin banka adi zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.HesapNo) && string.IsNullOrWhiteSpace(dto.Iban))
            {
                throw new BaseException("Banka/Doviz tipi hesap icin hesap no veya IBAN zorunludur.", 400);
            }
        }

        if (dto.Tip == KasaBankaHesapTipleri.KrediKarti)
        {
            if (string.IsNullOrWhiteSpace(dto.KartAdi) && string.IsNullOrWhiteSpace(dto.Ad))
            {
                throw new BaseException("Kredi karti icin kart adi veya ad zorunludur.", 400);
            }

            if (dto.KartLimiti.HasValue && dto.KartLimiti.Value < 0)
            {
                throw new BaseException("Kart limiti negatif olamaz.", 400);
            }

            if (dto.HesapKesimGunu.HasValue && (dto.HesapKesimGunu < 1 || dto.HesapKesimGunu > 31))
            {
                throw new BaseException("Hesap kesim gunu 1-31 araliginda olmalidir.", 400);
            }

            if (dto.SonOdemeGunu.HasValue && (dto.SonOdemeGunu < 1 || dto.SonOdemeGunu > 31))
            {
                throw new BaseException("Son odeme gunu 1-31 araliginda olmalidir.", 400);
            }

            if (dto.BagliBankaHesapId.HasValue)
            {
                var bagliBanka = await _dbContext.KasaBankaHesaplari
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == dto.BagliBankaHesapId.Value);
                if (bagliBanka is null || (bagliBanka.Tip != KasaBankaHesapTipleri.Banka && bagliBanka.Tip != KasaBankaHesapTipleri.DovizHesabi))
                {
                    throw new BaseException("Bagli banka hesabi gecersiz.", 400);
                }
            }
        }
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    private void NormalizeBasicFields(KasaBankaHesapDto dto)
    {
        dto.Tip = (dto.Tip ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
    }

    private async Task<int?> ResolveWriteTesisIdAsync(int? tesisId, int? existingId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var candidateTesisId = tesisId;

        if (!candidateTesisId.HasValue && existingId.HasValue)
        {
            candidateTesisId = await _repository.Where(x => x.Id == existingId.Value).Select(x => x.TesisId).FirstOrDefaultAsync();
        }

        if (scope.IsScoped)
        {
            if (!candidateTesisId.HasValue)
            {
                if (scope.TesisIds.Count == 1)
                {
                    candidateTesisId = scope.TesisIds.First();
                }
                else
                {
                    throw new BaseException("Tesis secimi zorunludur.", 400);
                }
            }

            if (!scope.TesisIds.Contains(candidateTesisId.Value))
            {
                throw new BaseException("Secilen tesis icin yetkiniz bulunmuyor.", 403);
            }
        }

        if (candidateTesisId.HasValue && candidateTesisId.Value > 0)
        {
            var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == candidateTesisId.Value && x.AktifMi);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        return candidateTesisId is > 0 ? candidateTesisId : null;
    }

    private async Task EnsureCanAccessTesisAsync(int? tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped)
        {
            return;
        }

        if (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value))
        {
            throw new BaseException("Bu kayda erisim yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }

            return result;
        };
    }
}
