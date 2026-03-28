using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using STYS.Fiyatlandirma.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri.Repositories;
using STYS.MisafirTipleri.Repositories;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Fiyatlandirma.Services;

public class OdaFiyatService : BaseRdbmsService<OdaFiyatDto, OdaFiyat, int>, IOdaFiyatService
{
    private readonly IOdaFiyatRepository _odaFiyatRepository;
    private readonly IOdaTipiRepository _odaTipiRepository;
    private readonly IKonaklamaTipiRepository _konaklamaTipiRepository;
    private readonly IMisafirTipiRepository _misafirTipiRepository;
    private readonly IIndirimKuraliRepository _indirimKuraliRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public OdaFiyatService(
        IOdaFiyatRepository odaFiyatRepository,
        IOdaTipiRepository odaTipiRepository,
        IKonaklamaTipiRepository konaklamaTipiRepository,
        IMisafirTipiRepository misafirTipiRepository,
        IIndirimKuraliRepository indirimKuraliRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysAppDbContext,
        IMapper mapper)
        : base(odaFiyatRepository, mapper)
    {
        _odaFiyatRepository = odaFiyatRepository;
        _odaTipiRepository = odaTipiRepository;
        _konaklamaTipiRepository = konaklamaTipiRepository;
        _misafirTipiRepository = misafirTipiRepository;
        _indirimKuraliRepository = indirimKuraliRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysAppDbContext;
    }

    public async Task<List<OdaFiyatDto>> GetByTesisOdaTipiIdAsync(int tesisOdaTipiId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessOdaTipiAsync(tesisOdaTipiId, cancellationToken);

        var list = await _odaFiyatRepository.Where(x => x.TesisOdaTipiId == tesisOdaTipiId)
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.KonaklamaTipiId)
            .ThenBy(x => x.MisafirTipiId)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<OdaFiyatDto>>(list);
    }

    public async Task<List<OdaFiyatDto>> UpsertByTesisOdaTipiAsync(int tesisOdaTipiId, IEnumerable<OdaFiyatDto> fiyatlar, CancellationToken cancellationToken = default)
    {
        var odaTipi = await EnsureCanAccessOdaTipiAsync(tesisOdaTipiId, cancellationToken);
        var items = (fiyatlar ?? []).ToList();

        foreach (var item in items)
        {
            item.TesisOdaTipiId = tesisOdaTipiId;
            item.KisiSayisi = 1;
            Normalize(item);
        }

        EnsureNoDuplicateRows(items);
        await EnsureReferencesAsync(items, odaTipi.TesisId, cancellationToken);

        var existing = await _odaFiyatRepository.Where(x => x.TesisOdaTipiId == tesisOdaTipiId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _odaFiyatRepository.DeleteRange(existing);
            await _odaFiyatRepository.SaveChangesAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            var entity = Mapper.Map<OdaFiyat>(item);
            entity.Id = 0;
            await _odaFiyatRepository.AddAsync(entity);
        }

        await _odaFiyatRepository.SaveChangesAsync(cancellationToken);
        return await GetByTesisOdaTipiIdAsync(tesisOdaTipiId, cancellationToken);
    }

    public async Task<OdaFiyatHesaplamaSonucuDto> HesaplaAsync(OdaFiyatHesaplaRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var odaTipi = await EnsureCanAccessOdaTipiAsync(request.TesisOdaTipiId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(odaTipi.TesisId, request.KonaklamaTipiId, cancellationToken);
        var hedefTarih = (request.Tarih ?? DateTime.UtcNow).Date;

        var basePrice = await _odaFiyatRepository.Where(x =>
                x.TesisOdaTipiId == request.TesisOdaTipiId &&
                x.KonaklamaTipiId == request.KonaklamaTipiId &&
                x.MisafirTipiId == request.MisafirTipiId &&
                x.KisiSayisi == 1 &&
                x.AktifMi &&
                x.BaslangicTarihi <= hedefTarih &&
                x.BitisTarihi >= hedefTarih)
            .OrderByDescending(x => x.BaslangicTarihi)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (basePrice is null)
        {
            throw new BaseException("Bu kriterler icin aktif bir baz fiyat bulunamadi.", 404);
        }

        var candidateRules = await _indirimKuraliRepository.Where(
                x => x.AktifMi &&
                    x.BaslangicTarihi <= hedefTarih &&
                    x.BitisTarihi >= hedefTarih &&
                    (x.KapsamTipi == IndirimKapsamTipleri.Sistem ||
                     (x.KapsamTipi == IndirimKapsamTipleri.Tesis && x.TesisId == odaTipi.TesisId)),
                query => query
                    .Include(x => x.MisafirTipiKisitlari)
                    .Include(x => x.KonaklamaTipiKisitlari))
            .ToListAsync(cancellationToken);

        var applicableRules = candidateRules
            .Where(x => IsRuleApplicable(x, request))
            .OrderBy(x => x.KapsamTipi.Equals(IndirimKapsamTipleri.Tesis, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(x => x.Oncelik)
            .ThenBy(x => x.Id)
            .ToList();

        var currentAmount = basePrice.Fiyat * request.KisiSayisi;
        var totalBaseAmount = currentAmount;
        var applied = new List<UygulananIndirimDto>();

        foreach (var rule in applicableRules)
        {
            var discountAmount = CalculateDiscountAmount(rule, currentAmount);
            if (discountAmount <= 0)
            {
                continue;
            }

            currentAmount -= discountAmount;
            applied.Add(new UygulananIndirimDto
            {
                IndirimKuraliId = rule.Id,
                KuralAdi = rule.Ad,
                IndirimTutari = discountAmount,
                SonrasiTutar = currentAmount
            });

            if (!rule.BirlesebilirMi)
            {
                break;
            }
        }

        return new OdaFiyatHesaplamaSonucuDto
        {
            BazFiyat = totalBaseAmount,
            NihaiFiyat = currentAmount,
            ParaBirimi = basePrice.ParaBirimi,
            UygulananIndirimler = applied
        };
    }

    private static bool IsRuleApplicable(IndirimKurali rule, OdaFiyatHesaplaRequestDto request)
    {
        if (rule.KonaklamaTipiKisitlari.Count > 0 && !rule.KonaklamaTipiKisitlari.Any(x => x.KonaklamaTipiId == request.KonaklamaTipiId))
        {
            return false;
        }

        if (rule.MisafirTipiKisitlari.Count > 0 && !rule.MisafirTipiKisitlari.Any(x => x.MisafirTipiId == request.MisafirTipiId))
        {
            return false;
        }

        return true;
    }

    private static decimal CalculateDiscountAmount(IndirimKurali rule, decimal currentAmount)
    {
        if (currentAmount <= 0 || rule.Deger <= 0)
        {
            return 0;
        }

        decimal discount = rule.IndirimTipi.Equals(IndirimTipleri.Yuzde, StringComparison.OrdinalIgnoreCase)
            ? Math.Round(currentAmount * rule.Deger / 100m, 2, MidpointRounding.AwayFromZero)
            : rule.Deger;

        if (discount < 0)
        {
            return 0;
        }

        return Math.Min(currentAmount, discount);
    }

    private async Task<OdaTipi> EnsureCanAccessOdaTipiAsync(int tesisOdaTipiId, CancellationToken cancellationToken)
    {
        var odaTipi = await _odaTipiRepository.GetByIdAsync(tesisOdaTipiId);
        if (odaTipi is null)
        {
            throw new BaseException("Tesis oda tipi bulunamadi.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(odaTipi.TesisId))
        {
            throw new BaseException("Bu tesis oda tipi altinda islem yapma yetkiniz bulunmuyor.", 403);
        }

        return odaTipi;
    }

    private async Task EnsureReferencesAsync(IEnumerable<OdaFiyatDto> fiyatlar, int tesisId, CancellationToken cancellationToken)
    {
        var konaklamaTipiIds = fiyatlar.Select(x => x.KonaklamaTipiId).Distinct().ToList();
        var misafirTipiIds = fiyatlar.Select(x => x.MisafirTipiId).Distinct().ToList();
        var odaTipiIds = fiyatlar.Select(x => x.TesisOdaTipiId).Distinct().ToList();

        var existingKonaklamaTipiIds = await _konaklamaTipiRepository.Where(x => konaklamaTipiIds.Contains(x.Id) && x.AktifMi)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var existingMisafirTipiIds = await _misafirTipiRepository.Where(x => misafirTipiIds.Contains(x.Id) && x.AktifMi)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var existingOdaTipiIds = await _odaTipiRepository.Where(x => odaTipiIds.Contains(x.Id) && x.AktifMi)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingKonaklamaTipiIds.Count != konaklamaTipiIds.Count)
        {
            throw new BaseException("Gecersiz veya pasif konaklama tipi secildi.", 400);
        }

        var tesisKonaklamaTipiIds = await _stysDbContext.TesisKonaklamaTipleri
            .Where(x => x.TesisId == tesisId
                && x.AktifMi
                && !x.IsDeleted
                && konaklamaTipiIds.Contains(x.KonaklamaTipiId))
            .Select(x => x.KonaklamaTipiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tesisKonaklamaTipiIds.Count != konaklamaTipiIds.Count)
        {
            throw new BaseException("Secilen konaklama tipi ilgili tesiste kullanima acik degil.", 400);
        }

        if (existingMisafirTipiIds.Count != misafirTipiIds.Count)
        {
            throw new BaseException("Gecersiz veya pasif misafir tipi secildi.", 400);
        }

        if (existingOdaTipiIds.Count != odaTipiIds.Count)
        {
            throw new BaseException("Gecersiz veya pasif tesis oda tipi secildi.", 400);
        }
    }

    private static void EnsureNoDuplicateRows(IReadOnlyCollection<OdaFiyatDto> fiyatlar)
    {
        var duplicates = fiyatlar
            .GroupBy(x => new { x.TesisOdaTipiId, x.KonaklamaTipiId, x.MisafirTipiId, x.KisiSayisi, Baslangic = x.BaslangicTarihi.Date, Bitis = x.BitisTarihi.Date })
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicates is not null)
        {
            throw new BaseException("Ayni oda tipi/konaklama/misafir/kisi ve tarih araligina ait birden fazla fiyat tanimi olamaz.", 400);
        }
    }

    private static void ValidateRequest(OdaFiyatHesaplaRequestDto request)
    {
        if (request.TesisOdaTipiId <= 0)
        {
            throw new BaseException("Tesis oda tipi secimi zorunludur.", 400);
        }

        if (request.KonaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (request.MisafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }
    }

    private static void Normalize(OdaFiyatDto dto)
    {
        if (dto.TesisOdaTipiId <= 0)
        {
            throw new BaseException("Tesis oda tipi secimi zorunludur.", 400);
        }

        if (dto.KonaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (dto.MisafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        dto.KisiSayisi = 1;

        if (dto.Fiyat < 0)
        {
            throw new BaseException("Fiyat sifirdan kucuk olamaz.", 400);
        }

        if (dto.BaslangicTarihi.Date > dto.BitisTarihi.Date)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
        }

        dto.ParaBirimi = string.IsNullOrWhiteSpace(dto.ParaBirimi)
            ? "TRY"
            : dto.ParaBirimi.Trim().ToUpperInvariant();
    }

    private async Task EnsureTesisHasKonaklamaTipiAsync(int tesisId, int konaklamaTipiId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.TesisKonaklamaTipleri.AnyAsync(x =>
            x.TesisId == tesisId
            && x.KonaklamaTipiId == konaklamaTipiId
            && x.AktifMi
            && !x.IsDeleted,
            cancellationToken);

        if (!exists)
        {
            throw new BaseException("Secilen konaklama tipi ilgili tesiste kullanima acik degil.", 400);
        }
    }
}
