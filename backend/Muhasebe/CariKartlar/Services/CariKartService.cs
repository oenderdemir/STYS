using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariKartlar.Services;

public class CariKartService : BaseRdbmsService<CariKartDto, CariKart, int>, ICariKartService
{

    private readonly ICariKartRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public CariKartService(ICariKartRepository repository, StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var cari = await GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        await EnsureCanAccessTesisAsync(cari.TesisId, cancellationToken);
        var hareketler = await _dbContext.CariHareketler
            .Where(x => x.CariKartId == cariKartId && x.Durum == CariHareketDurumlari.Aktif)
            .ToListAsync(cancellationToken);

        var toplamBorc = hareketler.Sum(x => x.BorcTutari);
        var toplamAlacak = hareketler.Sum(x => x.AlacakTutari);
        return new CariBakiyeDto
        {
            CariKartId = cariKartId,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Bakiye = toplamBorc - toplamAlacak,
            ParaBirimi = hareketler.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };
    }

    public override async Task<CariKartDto> AddAsync(CariKartDto dto)
    {
        var cancellationToken = CancellationToken.None;
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        NormalizeCommonFields(dto);

        var anaHesapKodu = ResolveAnaHesapKodu(dto.CariTipi);
        if (anaHesapKodu is null)
        {
            if (string.IsNullOrWhiteSpace(dto.CariKodu))
            {
                throw new BaseException("Cari kodu zorunludur.", 400);
            }

            dto.CariKodu = dto.CariKodu.Trim().ToUpperInvariant();
            var exists = await _repository.AnyAsync(x => x.CariKodu.ToUpper() == dto.CariKodu.ToUpper() && x.TesisId == dto.TesisId);
            if (exists)
            {
                throw new BaseException("Cari kodu ayni tesis altinda benzersiz olmalidir.", 400);
            }

            return await base.AddAsync(dto);
        }

        if (!dto.TesisId.HasValue || dto.TesisId.Value <= 0)
        {
            throw new BaseException("Tedarikci/Musteri/Kurumsal Musteri icin tesis secimi zorunludur.", 400);
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var anaHesap = await GetAnaHesapAsync(anaHesapKodu, dto.CariTipi, cancellationToken);
                var siraNo = await NextSiraNoAsync(dto.TesisId.Value, anaHesapKodu, cancellationToken);
                var uretilenKod = $"{anaHesapKodu}.{siraNo}";

                var existingWithSameCode = await _dbContext.CariKartlar
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.TesisId == dto.TesisId && x.CariKodu == uretilenKod && !x.IsDeleted, cancellationToken);
                if (existingWithSameCode)
                {
                    throw new BaseException("Uretilen cari kodu zaten mevcut. Islem tekrar deneyiniz.", 409);
                }

                var detayHesap = await ResolveOrCreateDetayHesapAsync(uretilenKod, dto.UnvanAdSoyad, dto.TesisId.Value, anaHesap, cancellationToken);

                var entity = new CariKart
                {
                    TesisId = dto.TesisId,
                    CariTipi = dto.CariTipi,
                    CariKodu = uretilenKod,
                    UnvanAdSoyad = dto.UnvanAdSoyad,
                    VergiNoTckn = NormalizeOptional(dto.VergiNoTckn, 32),
                    VergiDairesi = NormalizeOptional(dto.VergiDairesi, 128),
                    Telefon = NormalizeOptional(dto.Telefon, 32),
                    Eposta = NormalizeOptional(dto.Eposta, 256),
                    Adres = NormalizeOptional(dto.Adres, 512),
                    Il = NormalizeOptional(dto.Il, 128),
                    Ilce = NormalizeOptional(dto.Ilce, 128),
                    AktifMi = dto.AktifMi,
                    EFaturaMukellefiMi = dto.EFaturaMukellefiMi,
                    EArsivKapsamindaMi = dto.EArsivKapsamindaMi,
                    Aciklama = NormalizeOptional(dto.Aciklama, 1024),
                    AnaMuhasebeHesapKodu = anaHesapKodu,
                    MuhasebeHesapSiraNo = siraNo,
                    MuhasebeHesapPlaniId = detayHesap.Id
                };

                await _dbContext.CariKartlar.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Mapper.Map<CariKartDto>(entity);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 5)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < 5 && IsRetryableSqlConflict(ex))
            {
                await tx.RollbackAsync(cancellationToken);
            }
        }

        throw new BaseException("Cari kodu uretilirken eszamanli islem catismasi olustu. Tekrar deneyiniz.", 409);
    }

    public override async Task<CariKartDto> UpdateAsync(CariKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari kart id zorunludur.", 400);
        }

        NormalizeCommonFields(dto);
        var entity = await _dbContext.CariKartlar.FirstOrDefaultAsync(x => x.Id == dto.Id.Value)
            ?? throw new BaseException("Cari kart bulunamadi.", 404);

        await EnsureCanAccessTesisAsync(entity.TesisId, CancellationToken.None);

        var nextTesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id.Value);
        var hasMuhasebeLink = entity.MuhasebeHesapPlaniId.HasValue;

        if (hasMuhasebeLink && !string.Equals(entity.CariTipi, dto.CariTipi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş cari kartlarda cari tipi değiştirilemez.", 400);
        }

        if (hasMuhasebeLink && entity.TesisId != nextTesisId)
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş cari kartlarda tesis değiştirilemez.", 400);
        }

        entity.TesisId = nextTesisId;
        entity.CariTipi = dto.CariTipi;
        entity.UnvanAdSoyad = dto.UnvanAdSoyad;
        entity.VergiNoTckn = NormalizeOptional(dto.VergiNoTckn, 32);
        entity.VergiDairesi = NormalizeOptional(dto.VergiDairesi, 128);
        entity.Telefon = NormalizeOptional(dto.Telefon, 32);
        entity.Eposta = NormalizeOptional(dto.Eposta, 256);
        entity.Adres = NormalizeOptional(dto.Adres, 512);
        entity.Il = NormalizeOptional(dto.Il, 128);
        entity.Ilce = NormalizeOptional(dto.Ilce, 128);
        entity.AktifMi = dto.AktifMi;
        entity.EFaturaMukellefiMi = dto.EFaturaMukellefiMi;
        entity.EArsivKapsamindaMi = dto.EArsivKapsamindaMi;
        entity.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        if (entity.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == entity.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.Ad = entity.UnvanAdSoyad;
                if (!entity.AktifMi)
                {
                    hesap.AktifMi = false;
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        return Mapper.Map<CariKartDto>(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _dbContext.CariKartlar.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            throw new BaseException("Cari kart bulunamadi.", 404);
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

    public override async Task<CariKartDto?> GetByIdAsync(int id, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<CariKartDto>> GetAllAsync(Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<CariKartDto>> WhereAsync(System.Linq.Expressions.Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<CariKartDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<CariKart, bool>>? predicate = null,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null,
        Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task<int> NextSiraNoAsync(int tesisId, string anaHesapKodu, CancellationToken cancellationToken)
    {
        var sayac = await _dbContext.Set<MuhasebeHesapKoduSayac>()
            .FirstOrDefaultAsync(x => x.TesisId == tesisId && x.AnaHesapKodu == anaHesapKodu, cancellationToken);

        if (sayac is null)
        {
            sayac = new MuhasebeHesapKoduSayac
            {
                TesisId = tesisId,
                AnaHesapKodu = anaHesapKodu,
                SonSiraNo = 0,
                Aciklama = "Cari kart otomatik kod sayaci"
            };
            await _dbContext.Set<MuhasebeHesapKoduSayac>().AddAsync(sayac, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        sayac.SonSiraNo += 1;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return sayac.SonSiraNo;
    }

    private async Task<MuhasebeHesapPlani> GetAnaHesapAsync(string anaHesapKodu, string cariTipi, CancellationToken cancellationToken)
    {
        var anaHesap = await _dbContext.MuhasebeHesapPlanlari
            .Where(x => !x.IsDeleted && x.AktifMi)
            .FirstOrDefaultAsync(x => x.TesisId == null && (x.TamKod == anaHesapKodu || x.Kod == anaHesapKodu), cancellationToken);

        if (anaHesap is not null)
        {
            return anaHesap;
        }

        if (string.Equals(cariTipi, CariKartTipleri.Tedarikci, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("3.32.320 SATICILAR ana hesabı bulunamadı.", 400);
        }

        throw new BaseException("1.12.120 ALICILAR ana hesabı bulunamadı.", 400);
    }

    private async Task<MuhasebeHesapPlani> ResolveOrCreateDetayHesapAsync(
        string cariKodu,
        string unvanAdSoyad,
        int tesisId,
        MuhasebeHesapPlani anaHesap,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.TesisId == tesisId && (x.Kod == cariKodu || x.TamKod == cariKodu), cancellationToken);

        if (existing is not null)
        {
            var linkedToAnotherCari = await _dbContext.CariKartlar.AnyAsync(
                x => !x.IsDeleted && x.MuhasebeHesapPlaniId == existing.Id,
                cancellationToken);
            if (linkedToAnotherCari)
            {
                throw new BaseException($"'{cariKodu}' kodlu muhasebe hesap plani baska bir cari karta bagli.", 400);
            }

            existing.Ad = unvanAdSoyad;
            existing.AktifMi = true;
            return existing;
        }

        var detay = new MuhasebeHesapPlani
        {
            TesisId = tesisId,
            Kod = cariKodu,
            TamKod = cariKodu,
            Ad = unvanAdSoyad,
            SeviyeNo = anaHesap.SeviyeNo + 1,
            UstHesapId = anaHesap.Id,
            AktifMi = true,
            Aciklama = "Cari kart otomatik detay hesabi"
        };

        await _dbContext.MuhasebeHesapPlanlari.AddAsync(detay, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return detay;
    }

    private static string? ResolveAnaHesapKodu(string cariTipi)
    {
        if (string.Equals(cariTipi, CariKartTipleri.Tedarikci, StringComparison.OrdinalIgnoreCase))
        {
            return MuhasebeAnaHesapKodlari.CariTedarikci;
        }

        if (string.Equals(cariTipi, CariKartTipleri.Musteri, StringComparison.OrdinalIgnoreCase)
            || string.Equals(cariTipi, CariKartTipleri.KurumsalMusteri, StringComparison.OrdinalIgnoreCase))
        {
            return MuhasebeAnaHesapKodlari.CariMusteri;
        }

        return null;
    }

    private static void NormalizeCommonFields(CariKartDto dto)
    {
        dto.CariTipi = (dto.CariTipi ?? string.Empty).Trim();
        dto.UnvanAdSoyad = (dto.UnvanAdSoyad ?? string.Empty).Trim();
        dto.CariKodu = (dto.CariKodu ?? string.Empty).Trim();
        dto.VergiNoTckn = NormalizeOptional(dto.VergiNoTckn, 32);
        dto.VergiDairesi = NormalizeOptional(dto.VergiDairesi, 128);
        dto.Telefon = NormalizeOptional(dto.Telefon, 32);
        dto.Eposta = NormalizeOptional(dto.Eposta, 256);
        dto.Adres = NormalizeOptional(dto.Adres, 512);
        dto.Il = NormalizeOptional(dto.Il, 128);
        dto.Ilce = NormalizeOptional(dto.Ilce, 128);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        if (!CariKartTipleri.Hepsi.Contains(dto.CariTipi))
        {
            throw new BaseException("Cari tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.UnvanAdSoyad))
        {
            throw new BaseException("Unvan/Ad Soyad zorunludur.", 400);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static bool IsRetryableSqlConflict(DbUpdateException ex)
    {
        var sqlEx = ex.InnerException as SqlException;
        if (sqlEx is null)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627;
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

    private static Func<IQueryable<CariKart>, IQueryable<CariKart>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include)
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
