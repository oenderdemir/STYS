using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariKartlar.Services;

public class CariKartService : BaseRdbmsService<CariKartDto, CariKart, int>, ICariKartService
{

    private readonly ICariKartRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IMuhasebeDetayHesapService _muhasebeDetayHesapService;

    public CariKartService(
        ICariKartRepository repository,
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IMuhasebeDetayHesapService muhasebeDetayHesapService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _muhasebeDetayHesapService = muhasebeDetayHesapService;
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
        var acilisTutar = cari.AcilisBakiyeTutari.GetValueOrDefault();
        if (acilisTutar > 0m)
        {
            if (string.Equals(cari.AcilisBakiyeYonu, CariKartAcilisBakiyeYonleri.Borc, StringComparison.OrdinalIgnoreCase))
            {
                toplamBorc += acilisTutar;
            }
            else if (string.Equals(cari.AcilisBakiyeYonu, CariKartAcilisBakiyeYonleri.Alacak, StringComparison.OrdinalIgnoreCase))
            {
                toplamAlacak += acilisTutar;
            }
        }

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
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        NormalizeCommonFields(dto);
        NormalizeFinansFields(dto);
        var yetkiliKisiler = NormalizeYetkiliKisiler(dto.YetkiliKisiler);
        dto.YetkiliKisiler = [];

        var anaHesapKodu = ResolveAnaHesapKodu(dto.CariTipi);
        await using var tx = await _dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
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
            }
            else
            {
                if (!dto.TesisId.HasValue || dto.TesisId.Value <= 0)
                {
                    throw new BaseException("Tedarikci/Musteri/Kurumsal Musteri icin tesis secimi zorunludur.", 400);
                }

                var detay = await _muhasebeDetayHesapService.CreateOrResolveDetayHesapAsync(
                    dto.TesisId.Value,
                    anaHesapKodu,
                    "CariKart",
                    dto.UnvanAdSoyad,
                    null,
                    CancellationToken.None);

                dto.MuhasebeHesapPlaniId = detay.MuhasebeHesapPlaniId;
                dto.AnaMuhasebeHesapKodu = detay.AnaMuhasebeHesapKodu;
                dto.MuhasebeHesapSiraNo = detay.SiraNo;
                dto.CariKodu = detay.Kod;
            }

            var result = await base.AddAsync(dto);
            var resultId = result.Id ?? 0;
            if (resultId > 0)
            {
                await SyncYetkiliKisileriAsync(resultId, yetkiliKisiler, CancellationToken.None);
            }

            await tx.CommitAsync(CancellationToken.None);
            return await GetByIdAsync(resultId) ?? result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task<CariKartDto> UpdateAsync(CariKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari kart id zorunludur.", 400);
        }

        NormalizeCommonFields(dto);
        NormalizeFinansFields(dto);
        var yetkiliKisiler = NormalizeYetkiliKisiler(dto.YetkiliKisiler);
        dto.YetkiliKisiler = [];
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
        entity.AcilisBakiyeTutari = NormalizeAcilisBakiyeTutari(dto.AcilisBakiyeTutari);
        entity.AcilisBakiyeYonu = NormalizeAcilisBakiyeYonu(dto.AcilisBakiyeTutari, dto.AcilisBakiyeYonu);
        entity.AcilisBakiyeTarihi = entity.AcilisBakiyeTutari.GetValueOrDefault() > 0m ? dto.AcilisBakiyeTarihi : null;
        entity.BankaAdi = NormalizeOptional(dto.BankaAdi, 128);
        entity.Iban = NormalizeIban(dto.Iban);

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

        await using var tx = await _dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
            var mevcutYetkililer = await _dbContext.CariKartYetkiliKisileri
                .Where(x => x.CariKartId == entity.Id)
                .ToListAsync(CancellationToken.None);
            _dbContext.CariKartYetkiliKisileri.RemoveRange(mevcutYetkililer);
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            await SyncYetkiliKisileriAsync(entity.Id, yetkiliKisiler, CancellationToken.None);

            await tx.CommitAsync(CancellationToken.None);
            return await GetByIdAsync(entity.Id) ?? Mapper.Map<CariKartDto>(entity);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
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
        Func<IQueryable<CariKart>, IQueryable<CariKart>> includeWithChildren = q => include is null
            ? q.Include(x => x.YetkiliKisiler)
            : include(q).Include(x => x.YetkiliKisiler);
        var includeQuery = BuildScopedIncludeQuery(scope, includeWithChildren);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<CariKartDto>> GetAllAsync(Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public async Task<IEnumerable<CariKartDto>> GetAllAsync(
        int? tesisId,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include, tesisId);
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

    public async Task<PagedResult<CariKartDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null,
        Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include, tesisId);
        return await base.GetPagedAsync(request, null, includeQuery, orderBy);
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

    private static void NormalizeFinansFields(CariKartDto dto)
    {
        if (dto.AcilisBakiyeTutari.HasValue && dto.AcilisBakiyeTutari.Value < 0m)
        {
            throw new BaseException("Açılış bakiyesi negatif olamaz.", 400);
        }

        dto.AcilisBakiyeTutari = NormalizeAcilisBakiyeTutari(dto.AcilisBakiyeTutari);
        dto.AcilisBakiyeYonu = NormalizeAcilisBakiyeYonu(dto.AcilisBakiyeTutari, dto.AcilisBakiyeYonu);
        dto.BankaAdi = NormalizeOptional(dto.BankaAdi, 128);
        dto.Iban = NormalizeIban(dto.Iban);

        if (dto.AcilisBakiyeTutari.GetValueOrDefault() > 0m)
        {
            if (!dto.AcilisBakiyeTarihi.HasValue)
            {
                throw new BaseException("Açılış bakiyesi için tarih zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.AcilisBakiyeYonu))
            {
                throw new BaseException("Açılış bakiyesi için yön zorunludur.", 400);
            }
        }
        else
        {
            dto.AcilisBakiyeTarihi = null;
            dto.AcilisBakiyeYonu = null;
        }

        ValidateYetkiliKisiler(dto.YetkiliKisiler);
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

    private static decimal? NormalizeAcilisBakiyeTutari(decimal? value)
        => value.GetValueOrDefault() > 0m ? value.GetValueOrDefault() : value;

    private static string? NormalizeAcilisBakiyeYonu(decimal? tutar, string? value)
    {
        if (tutar.GetValueOrDefault() <= 0m)
        {
            return null;
        }

        return NormalizeAcilisBakiyeYonu(value);
    }

    private static string? NormalizeAcilisBakiyeYonu(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, CariKartAcilisBakiyeYonleri.Borc, StringComparison.OrdinalIgnoreCase))
        {
            return CariKartAcilisBakiyeYonleri.Borc;
        }

        if (string.Equals(normalized, CariKartAcilisBakiyeYonleri.Alacak, StringComparison.OrdinalIgnoreCase))
        {
            return CariKartAcilisBakiyeYonleri.Alacak;
        }

        throw new BaseException("Açılış bakiyesi yönü geçersiz.", 400);
    }

    private static string? NormalizeIban(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length > 34)
        {
            throw new BaseException("IBAN uzunluğu geçersiz.", 400);
        }

        return normalized;
    }

    private static void ValidateYetkiliKisiler(IEnumerable<CariKartYetkiliKisiDto>? yetkiliKisiler)
    {
        if (yetkiliKisiler is null)
        {
            return;
        }

        foreach (var kisi in yetkiliKisiler)
        {
            if (kisi is null)
            {
                continue;
            }

            var hasValue = !string.IsNullOrWhiteSpace(kisi.AdSoyad)
                || !string.IsNullOrWhiteSpace(kisi.GorevUnvan)
                || !string.IsNullOrWhiteSpace(kisi.Telefon)
                || !string.IsNullOrWhiteSpace(kisi.Eposta)
                || !string.IsNullOrWhiteSpace(kisi.Aciklama);

            if (!hasValue)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(kisi.AdSoyad))
            {
                throw new BaseException("Yetkili kişi için ad soyad zorunludur.", 400);
            }

            _ = NormalizeOptional(kisi.AdSoyad, 256);
            _ = NormalizeOptional(kisi.GorevUnvan, 128);
            _ = NormalizeOptional(kisi.Telefon, 32);
            _ = NormalizeEmail(kisi.Eposta);
            _ = NormalizeOptional(kisi.Aciklama, 1024);
        }
    }

    private static List<CariKartYetkiliKisiDto> NormalizeYetkiliKisiler(IEnumerable<CariKartYetkiliKisiDto>? yetkiliKisiler)
    {
        var result = new List<CariKartYetkiliKisiDto>();
        if (yetkiliKisiler is null)
        {
            return result;
        }

        foreach (var kisi in yetkiliKisiler)
        {
            if (kisi is null)
            {
                continue;
            }

            var hasValue = !string.IsNullOrWhiteSpace(kisi.AdSoyad)
                || !string.IsNullOrWhiteSpace(kisi.GorevUnvan)
                || !string.IsNullOrWhiteSpace(kisi.Telefon)
                || !string.IsNullOrWhiteSpace(kisi.Eposta)
                || !string.IsNullOrWhiteSpace(kisi.Aciklama);

            if (!hasValue)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(kisi.AdSoyad))
            {
                throw new BaseException("Yetkili kişi için ad soyad zorunludur.", 400);
            }

            result.Add(new CariKartYetkiliKisiDto
            {
                Id = kisi.Id,
                CariKartId = kisi.CariKartId,
                AdSoyad = NormalizeOptional(kisi.AdSoyad, 256) ?? string.Empty,
                GorevUnvan = NormalizeOptional(kisi.GorevUnvan, 128),
                Telefon = NormalizeOptional(kisi.Telefon, 32),
                Eposta = NormalizeEmail(kisi.Eposta),
                Aciklama = NormalizeOptional(kisi.Aciklama, 1024)
            });
        }

        return result;
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeOptional(value, 256);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            _ = new MailAddress(normalized);
            return normalized;
        }
        catch
        {
            throw new BaseException("Eposta formatı geçersiz.", 400);
        }
    }

    private async Task SyncYetkiliKisileriAsync(int cariKartId, IEnumerable<CariKartYetkiliKisiDto> yetkiliKisiler, CancellationToken cancellationToken)
    {
        var entities = yetkiliKisiler.Select(kisi => new CariKartYetkiliKisi
        {
            CariKartId = cariKartId,
            AdSoyad = kisi.AdSoyad,
            GorevUnvan = kisi.GorevUnvan,
            Telefon = kisi.Telefon,
            Eposta = kisi.Eposta,
            Aciklama = kisi.Aciklama
        }).ToList();

        if (entities.Count == 0)
        {
            return;
        }

        await _dbContext.CariKartYetkiliKisileri.AddRangeAsync(entities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include,
        int? tesisId = null)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }

            if (tesisId.HasValue && tesisId.Value > 0)
            {
                result = result.Where(x => x.TesisId == tesisId.Value);
            }

            return result;
        };
    }
}
