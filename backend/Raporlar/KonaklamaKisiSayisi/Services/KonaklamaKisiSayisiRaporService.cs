using System.Globalization;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.KonaklamaKisiSayisi.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.KonaklamaKisiSayisi.Services;

public class KonaklamaKisiSayisiRaporService : IKonaklamaKisiSayisiRaporService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    private static readonly string[] AyAdlariBuyukHarf =
    [
        "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN",
        "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK"
    ];

    private const int MaksimumYilAraligi = 10;

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public KonaklamaKisiSayisiRaporService(
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        ICurrentTenantAccessor currentTenantAccessor,
        IDomainOperationLogger domainLogger)
    {
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
        _currentTenantAccessor = currentTenantAccessor;
        _domainLogger = domainLogger;
    }

    public async Task<KonaklamaKisiSayisiRaporDto> GetRaporAsync(
        int tesisId,
        int ay,
        int baslangicYil,
        int bitisYil,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis id.", 400);
        }

        if (ay < 1 || ay > 12)
        {
            throw new BaseException("Ay degeri 1-12 araliginda olmalidir.", 400);
        }

        if (baslangicYil < 2000 || baslangicYil > 2100 || bitisYil < 2000 || bitisYil > 2100)
        {
            throw new BaseException("Yil degeri 2000-2100 araliginda olmalidir.", 400);
        }

        if (baslangicYil > bitisYil)
        {
            throw new BaseException("Baslangic yili bitis yilindan buyuk olamaz.", 400);
        }

        if (bitisYil - baslangicYil + 1 > MaksimumYilAraligi)
        {
            throw new BaseException($"Yil araligi en fazla {MaksimumYilAraligi} yil olabilir.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var odalar = await _stysDbContext.Odalar
            .AsNoTracking()
            .Where(o => o.AktifMi
                && o.Bina != null && o.Bina.AktifMi && o.Bina.TesisId == tesisId
                && o.TesisOdaTipi != null && o.TesisOdaTipi.AktifMi)
            .OrderBy(o => o.Bina!.Ad)
            .ThenBy(o => o.OdaNo)
            .Select(o => new KonaklamaKisiSayisiOdaDto
            {
                OdaId = o.Id,
                OdaNo = o.OdaNo,
                OdaTipiAdi = o.TesisOdaTipi!.Ad,
                Kapasite = o.TesisOdaTipi.Kapasite
            })
            .ToListAsync(cancellationToken);

        var odaIds = odalar.Select(x => x.OdaId).ToHashSet();

        var yillar = new List<KonaklamaKisiSayisiYilSatiriDto>();

        for (var yil = baslangicYil; yil <= bitisYil; yil++)
        {
            var ayBaslangic = new DateTime(yil, ay, 1);
            var ayBitisExclusive = ayBaslangic.AddMonths(1);

            // Bu rapor konaklayan kisi sayisini sayar; kisi/gece hesaplamasi yapilmaz.
            // Ayni rezervasyon/oda kombinasyonu ayda birden fazla segmentte gorunse bile
            // (RezervasyonId + OdaId) tekil anahtariyla yalnizca bir kez sayilir.
            var segmentKayitlari = await _stysDbContext.RezervasyonSegmentleri
                .AsNoTracking()
                .Where(s => s.BaslangicTarihi < ayBitisExclusive
                    && s.BitisTarihi > ayBaslangic
                    && s.Rezervasyon != null
                    && s.Rezervasyon.TesisId == tesisId
                    && s.Rezervasyon.AktifMi
                    && s.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
                .SelectMany(
                    s => s.OdaAtamalari.Where(a => odaIds.Contains(a.OdaId)),
                    (s, a) => new
                    {
                        s.RezervasyonId,
                        a.OdaId,
                        a.AyrilanKisiSayisi
                    })
                .ToListAsync(cancellationToken);

            var kisiSayisiByOdaId = segmentKayitlari
                .GroupBy(x => new { x.RezervasyonId, x.OdaId })
                .Select(g => g.First())
                .GroupBy(x => x.OdaId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.AyrilanKisiSayisi));

            var hucreler = odalar
                .Select(oda => new KonaklamaKisiSayisiHucreDto
                {
                    OdaId = oda.OdaId,
                    OdaNo = oda.OdaNo,
                    KisiSayisi = kisiSayisiByOdaId.GetValueOrDefault(oda.OdaId)
                })
                .ToList();

            yillar.Add(new KonaklamaKisiSayisiYilSatiriDto
            {
                Yil = yil,
                Hucreler = hucreler,
                ToplamKisiSayisi = hucreler.Sum(x => x.KisiSayisi)
            });
        }

        var ayAdi = AyAdlariBuyukHarf[ay - 1];
        var baslik = baslangicYil == bitisYil
            ? $"{baslangicYil} {ayAdi} AYI KONAKLAYAN KİŞİ SAYISI"
            : $"{baslangicYil}-{bitisYil} {ayAdi} AYI KONAKLAYAN KİŞİ SAYISI";

        return new KonaklamaKisiSayisiRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Ay = ay,
            AyAdi = ayAdi,
            BaslangicYil = baslangicYil,
            BitisYil = bitisYil,
            Baslik = baslik,
            Odalar = odalar,
            Yillar = yillar
        };
    }

    private async Task<string?> EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        var tesisQuery = _stysDbContext.Tesisler.Where(x => x.Id == tesisId && x.AktifMi);
        if (currentKurumId.HasValue)
        {
            tesisQuery = tesisQuery.Where(x => x.KurumId == currentKurumId.Value);
        }

        var tesis = await tesisQuery.Select(x => new { x.Ad }).FirstOrDefaultAsync(cancellationToken);

        if (tesis is null)
        {
            _domainLogger.Warning("Security.Tesis.AccessDenied", new
            {
                RequestedTesisId = tesisId,
                CurrentKurumId = currentKurumId
            });
            throw new BaseException(currentKurumId.HasValue
                ? "Bu tesis aktif kuruma ait degil."
                : "Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            _domainLogger.Warning("Security.Scope.AccessDenied", new
            {
                RequestedTesisId = tesisId,
                CurrentKurumId = currentKurumId
            });
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }

        return tesis.Ad;
    }
}
