using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.GecikenCheckIn.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.GecikenCheckIn.Services;

public class GecikenCheckInRaporService : IGecikenCheckInRaporService
{
    private static readonly HashSet<string> GecerliGecikmeDurumlari =
    [
        "tumu", "bugun-giris", "geciken", "kritik-geciken"
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public GecikenCheckInRaporService(
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

    public async Task<GecikenCheckInRaporDto> GetRaporAsync(
        int tesisId,
        DateTime? referansTarihi = null,
        int? odaTipiId = null,
        string? gecikmeDurumu = null,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis id.", 400);
        }

        if (odaTipiId.HasValue && odaTipiId.Value <= 0)
        {
            throw new BaseException("Gecersiz oda tipi id.", 400);
        }

        var gecikmeDurumuFiltre = string.IsNullOrWhiteSpace(gecikmeDurumu) ? "tumu" : gecikmeDurumu.Trim().ToLowerInvariant();
        if (!GecerliGecikmeDurumlari.Contains(gecikmeDurumuFiltre))
        {
            throw new BaseException("Gecersiz gecikme durumu filtresi.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        // Bugunun tarihi tek noktadan alinir; boylece testlerde referansTarihi verilerek
        // davranis DateTime.Now'a bagimli olmadan dogrulanabilir.
        var referansGun = (referansTarihi ?? DateTime.Now).Date;
        var referansGunSonrasi = referansGun.AddDays(1);

        string? odaTipiAdi = null;
        if (odaTipiId.HasValue)
        {
            odaTipiAdi = await _stysDbContext.Odalar
                .AsNoTracking()
                .Where(o => !o.IsDeleted
                    && o.AktifMi
                    && o.Bina != null && !o.Bina.IsDeleted && o.Bina.AktifMi && o.Bina.TesisId == tesisId
                    && o.TesisOdaTipi != null && !o.TesisOdaTipi.IsDeleted && o.TesisOdaTipi.AktifMi
                    && o.TesisOdaTipiId == odaTipiId.Value)
                .Select(o => o.TesisOdaTipi!.Ad)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Bu rapor sadece henuz check-in yapilmamis (Taslak/Onayli) rezervasyonlari gosterir;
        // check-in/check-out tamamlanmis ve iptal rezervasyonlar hric tutulur.
        var rezervasyonlar = await _stysDbContext.Rezervasyonlar
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.TesisId == tesisId
                && r.AktifMi
                && (r.RezervasyonDurumu == RezervasyonDurumlari.Taslak || r.RezervasyonDurumu == RezervasyonDurumlari.Onayli)
                && r.GirisTarihi < referansGunSonrasi)
            .Select(r => new
            {
                r.Id,
                r.ReferansNo,
                r.MisafirAdiSoyadi,
                r.MisafirTelefon,
                r.GirisTarihi,
                r.CikisTarihi,
                r.KisiSayisi,
                r.RezervasyonDurumu,
                r.ToplamUcret,
                r.ParaBirimi
            })
            .ToListAsync(cancellationToken);

        var rezervasyonIds = rezervasyonlar.Select(x => x.Id).ToList();

        var segmentOdaKayitlari = await _stysDbContext.RezervasyonSegmentleri
            .AsNoTracking()
            .Where(s => !s.IsDeleted && rezervasyonIds.Contains(s.RezervasyonId))
            .SelectMany(
                s => s.OdaAtamalari.Where(a => !a.IsDeleted
                    && a.Oda != null && !a.Oda.IsDeleted && a.Oda.AktifMi
                    && a.Oda.Bina != null && !a.Oda.Bina.IsDeleted && a.Oda.Bina.AktifMi
                    && a.Oda.TesisOdaTipi != null && !a.Oda.TesisOdaTipi.IsDeleted && a.Oda.TesisOdaTipi.AktifMi),
                (s, a) => new SegmentOdaKaydi
                {
                    RezervasyonId = s.RezervasyonId,
                    OdaNo = a.Oda!.OdaNo,
                    OdaTipiId = a.Oda.TesisOdaTipiId,
                    OdaTipiAdi = a.Oda.TesisOdaTipi!.Ad
                })
            .ToListAsync(cancellationToken);

        var segmentOdaByRezervasyonId = segmentOdaKayitlari
            .GroupBy(x => x.RezervasyonId)
            .ToDictionary(g => g.Key, g => g.ToList());

        HashSet<int>? odaTipiFiltresiUyanRezervasyonIds = null;
        if (odaTipiId.HasValue)
        {
            odaTipiFiltresiUyanRezervasyonIds = segmentOdaKayitlari
                .Where(x => x.OdaTipiId == odaTipiId.Value)
                .Select(x => x.RezervasyonId)
                .ToHashSet();
        }

        var odemeToplamlari = await _stysDbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(o => !o.IsDeleted && rezervasyonIds.Contains(o.RezervasyonId))
            .GroupBy(o => o.RezervasyonId)
            .Select(g => new { RezervasyonId = g.Key, Toplam = g.Sum(x => x.OdemeTutari) })
            .ToDictionaryAsync(x => x.RezervasyonId, cancellationToken);

        var filtrelenmisRezervasyonlar = new List<GecikenCheckInRezervasyonDto>();
        foreach (var r in rezervasyonlar)
        {
            if (odaTipiFiltresiUyanRezervasyonIds is not null && !odaTipiFiltresiUyanRezervasyonIds.Contains(r.Id))
            {
                continue;
            }

            var gecikenGunSayisi = (referansGun - r.GirisTarihi.Date).Days;
            if (gecikenGunSayisi < 0)
            {
                continue;
            }

            string gecikmeDurumuKodu;
            string gecikmeDurumuLabel;
            if (gecikenGunSayisi == 0)
            {
                gecikmeDurumuKodu = "bugun-giris";
                gecikmeDurumuLabel = "Bugün Giriş";
            }
            else if (gecikenGunSayisi <= 2)
            {
                gecikmeDurumuKodu = "geciken";
                gecikmeDurumuLabel = "Geciken";
            }
            else
            {
                gecikmeDurumuKodu = "kritik-geciken";
                gecikmeDurumuLabel = "Kritik Geciken";
            }

            if (gecikmeDurumuFiltre != "tumu" && gecikmeDurumuFiltre != gecikmeDurumuKodu)
            {
                continue;
            }

            var segmentKayitlari = segmentOdaByRezervasyonId.GetValueOrDefault(r.Id, []);
            if (odaTipiId.HasValue)
            {
                segmentKayitlari = segmentKayitlari
                    .Where(x => x.OdaTipiId == odaTipiId.Value)
                    .ToList();
            }

            var odenenTutar = odemeToplamlari.GetValueOrDefault(r.Id)?.Toplam ?? 0m;
            var kalanTutar = Math.Max(0m, r.ToplamUcret - odenenTutar);

            filtrelenmisRezervasyonlar.Add(new GecikenCheckInRezervasyonDto
            {
                RezervasyonId = r.Id,
                ReferansNo = r.ReferansNo,
                MisafirAdiSoyadi = r.MisafirAdiSoyadi,
                MisafirTelefon = r.MisafirTelefon,
                GirisTarihi = r.GirisTarihi,
                CikisTarihi = r.CikisTarihi,
                GecikenGunSayisi = gecikenGunSayisi,
                KisiSayisi = r.KisiSayisi,
                RezervasyonDurumu = r.RezervasyonDurumu,
                RezervasyonDurumuLabel = RezervasyonDurumuLabel(r.RezervasyonDurumu),
                GecikmeDurumu = gecikmeDurumuKodu,
                GecikmeDurumuLabel = gecikmeDurumuLabel,
                OdaNolari = segmentKayitlari.Select(x => x.OdaNo).Distinct().OrderBy(x => x).ToList(),
                OdaTipleri = segmentKayitlari.Select(x => x.OdaTipiAdi).Distinct().OrderBy(x => x).ToList(),
                ToplamUcret = r.ToplamUcret,
                OdenenTutar = odenenTutar,
                KalanTutar = kalanTutar,
                ParaBirimi = string.IsNullOrWhiteSpace(r.ParaBirimi) ? "TRY" : r.ParaBirimi
            });
        }

        var siralanmisRezervasyonlar = filtrelenmisRezervasyonlar
            .OrderByDescending(x => x.GecikenGunSayisi)
            .ThenBy(x => x.GirisTarihi)
            .ThenBy(x => x.MisafirAdiSoyadi)
            .ToList();

        var ozet = new GecikenCheckInOzetDto
        {
            ToplamRezervasyonSayisi = siralanmisRezervasyonlar.Count,
            BugunGirisSayisi = siralanmisRezervasyonlar.Count(x => x.GecikmeDurumu == "bugun-giris"),
            GecikenSayisi = siralanmisRezervasyonlar.Count(x => x.GecikmeDurumu == "geciken"),
            KritikGecikenSayisi = siralanmisRezervasyonlar.Count(x => x.GecikmeDurumu == "kritik-geciken"),
            ToplamKisiSayisi = siralanmisRezervasyonlar.Sum(x => x.KisiSayisi),
            ToplamKalanTutar = siralanmisRezervasyonlar.Sum(x => x.KalanTutar)
        };

        return new GecikenCheckInRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            ReferansTarihi = referansGun,
            OdaTipiId = odaTipiId,
            OdaTipiAdi = odaTipiAdi,
            GecikmeDurumu = gecikmeDurumuFiltre,
            Baslik = $"{referansGun:dd.MM.yyyy} GECİKEN CHECK-IN / GİRİŞ YAPMAYAN REZERVASYONLAR RAPORU",
            Ozet = ozet,
            Rezervasyonlar = siralanmisRezervasyonlar
        };
    }

    private static string RezervasyonDurumuLabel(string rezervasyonDurumu) => rezervasyonDurumu switch
    {
        RezervasyonDurumlari.Taslak => "Taslak",
        RezervasyonDurumlari.Onayli => "Onaylı",
        RezervasyonDurumlari.CheckInTamamlandi => "Check-in Tamamlandı",
        RezervasyonDurumlari.CheckOutTamamlandi => "Check-out Tamamlandı",
        RezervasyonDurumlari.Iptal => "İptal",
        _ => rezervasyonDurumu
    };

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

    private sealed class SegmentOdaKaydi
    {
        public int RezervasyonId { get; set; }
        public string OdaNo { get; set; } = string.Empty;
        public int OdaTipiId { get; set; }
        public string OdaTipiAdi { get; set; } = string.Empty;
    }
}
