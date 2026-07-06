using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.GunlukGirisCikis.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.GunlukGirisCikis.Services;

public class GunlukGirisCikisRaporService : IGunlukGirisCikisRaporService
{
    private static readonly HashSet<string> GecerliListeTipleri =
    [
        "tumu", "girisler", "cikislar", "devam-edenler", "geciken-cikislar"
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public GunlukGirisCikisRaporService(
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

    public async Task<GunlukGirisCikisRaporDto> GetRaporAsync(
        int tesisId,
        DateTime tarih,
        string? listeTipi = null,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis id.", 400);
        }

        var filtre = string.IsNullOrWhiteSpace(listeTipi) ? "tumu" : listeTipi.Trim().ToLowerInvariant();
        if (!GecerliListeTipleri.Contains(filtre))
        {
            throw new BaseException("Gecersiz liste tipi filtresi.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var gun = tarih.Date;
        var sonrakiGun = gun.AddDays(1);

        // Devam eden ve geciken cikis kayitlari gun'un gerisinde kalabilir; bu yuzden alt sinir
        // konulmaz, yalnizca GirisTarihi < sonrakiGun ile bu gunu ilgilendiremeyecek (ileri
        // tarihli) rezervasyonlar elenir.
        var rezervasyonlar = await _stysDbContext.Rezervasyonlar
            .AsNoTracking()
            .Where(r => r.TesisId == tesisId
                && r.AktifMi
                && r.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && r.GirisTarihi < sonrakiGun)
            .Select(r => new
            {
                r.Id,
                r.ReferansNo,
                r.MisafirAdiSoyadi,
                r.GirisTarihi,
                r.CikisTarihi,
                r.RezervasyonDurumu,
                r.KisiSayisi,
                r.ToplamUcret,
                r.ParaBirimi,
                r.Notlar
            })
            .ToListAsync(cancellationToken);

        var rezervasyonIds = rezervasyonlar.Select(x => x.Id).ToList();

        var odaAtamalari = await _stysDbContext.RezervasyonSegmentOdaAtamalari
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.RezervasyonSegment != null
                && !a.RezervasyonSegment.IsDeleted
                && rezervasyonIds.Contains(a.RezervasyonSegment.RezervasyonId)
                && a.RezervasyonSegment.BaslangicTarihi < sonrakiGun
                && a.RezervasyonSegment.BitisTarihi > gun)
            .Select(a => new { a.RezervasyonSegment!.RezervasyonId, a.OdaNoSnapshot })
            .ToListAsync(cancellationToken);

        var odaNolariByRezervasyonId = odaAtamalari
            .GroupBy(x => x.RezervasyonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OdaNoSnapshot).Distinct().OrderBy(x => x).ToList());

        var odemeToplamlari = await _stysDbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(o => !o.IsDeleted && rezervasyonIds.Contains(o.RezervasyonId))
            .GroupBy(o => o.RezervasyonId)
            .Select(g => new { RezervasyonId = g.Key, Toplam = g.Sum(x => x.OdemeTutari) })
            .ToDictionaryAsync(x => x.RezervasyonId, cancellationToken);

        // Borc durumu hesaplanirken rezervasyonun tum odeme kayitlari dikkate alinir; tarih sadece rapora girecek rezervasyonlari belirler.
        var tumRezervasyonlar = rezervasyonlar.Select(r =>
        {
            var odenenTutar = odemeToplamlari.GetValueOrDefault(r.Id)?.Toplam ?? 0m;
            var kalanTutar = Math.Max(0m, r.ToplamUcret - odenenTutar);

            var girisYapacakMi = r.GirisTarihi.Date == gun;
            var cikisYapacakMi = r.CikisTarihi.Date == gun;
            var devamEdiyorMu = r.GirisTarihi.Date < gun && r.CikisTarihi.Date > gun;
            var gecikenCikisMi = r.CikisTarihi.Date < gun
                && r.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi
                && r.RezervasyonDurumu != RezervasyonDurumlari.Iptal;

            string listeDurumu;
            string listeDurumuLabel;
            if (gecikenCikisMi)
            {
                listeDurumu = "geciken-cikis";
                listeDurumuLabel = "Geciken Çıkış";
            }
            else if (girisYapacakMi && cikisYapacakMi)
            {
                listeDurumu = "giris-cikis";
                listeDurumuLabel = "Giriş/Çıkış";
            }
            else if (girisYapacakMi)
            {
                listeDurumu = "giris";
                listeDurumuLabel = "Giriş";
            }
            else if (cikisYapacakMi)
            {
                listeDurumu = "cikis";
                listeDurumuLabel = "Çıkış";
            }
            else
            {
                listeDurumu = "devam-eden";
                listeDurumuLabel = "Devam Ediyor";
            }

            var odaNolari = odaNolariByRezervasyonId.GetValueOrDefault(r.Id, []);

            return new GunlukGirisCikisRezervasyonDto
            {
                RezervasyonId = r.Id,
                ReferansNo = r.ReferansNo,
                MisafirAdiSoyadi = r.MisafirAdiSoyadi,
                KurumUnite = null,
                GirisTarihi = r.GirisTarihi,
                CikisTarihi = r.CikisTarihi,
                RezervasyonDurumu = r.RezervasyonDurumu,
                RezervasyonDurumuLabel = RezervasyonDurumuLabel(r.RezervasyonDurumu),
                OdaNolari = odaNolari,
                KisiSayisi = r.KisiSayisi,
                ToplamUcret = r.ToplamUcret,
                OdenenTutar = odenenTutar,
                KalanTutar = kalanTutar,
                ParaBirimi = string.IsNullOrWhiteSpace(r.ParaBirimi) ? "TRY" : r.ParaBirimi,
                GirisYapacakMi = girisYapacakMi,
                CikisYapacakMi = cikisYapacakMi,
                DevamEdiyorMu = devamEdiyorMu,
                GecikenCikisMi = gecikenCikisMi,
                ListeDurumu = listeDurumu,
                ListeDurumuLabel = listeDurumuLabel,
                Aciklama = r.Notlar
            };
        })
        .Where(x => x.GirisYapacakMi || x.CikisYapacakMi || x.DevamEdiyorMu || x.GecikenCikisMi)
        .ToList();

        IEnumerable<GunlukGirisCikisRezervasyonDto> filtrelenmisSorgu = filtre switch
        {
            "girisler" => tumRezervasyonlar.Where(x => x.GirisYapacakMi),
            "cikislar" => tumRezervasyonlar.Where(x => x.CikisYapacakMi),
            "devam-edenler" => tumRezervasyonlar.Where(x => x.DevamEdiyorMu),
            "geciken-cikislar" => tumRezervasyonlar.Where(x => x.GecikenCikisMi),
            _ => tumRezervasyonlar
        };

        var siralanmisRezervasyonlar = filtrelenmisSorgu
            .OrderByDescending(x => x.GecikenCikisMi)
            .ThenByDescending(x => x.GirisYapacakMi)
            .ThenByDescending(x => x.CikisYapacakMi)
            .ThenBy(x => x.GirisTarihi)
            .ThenBy(x => x.OdaNolari.Count > 0 ? x.OdaNolari[0] : string.Empty)
            .ThenBy(x => x.MisafirAdiSoyadi)
            .ToList();

        var ozet = new GunlukGirisCikisOzetDto
        {
            GirisSayisi = siralanmisRezervasyonlar.Count(x => x.GirisYapacakMi),
            CikisSayisi = siralanmisRezervasyonlar.Count(x => x.CikisYapacakMi),
            DevamEdenSayisi = siralanmisRezervasyonlar.Count(x => x.DevamEdiyorMu),
            GecikenCikisSayisi = siralanmisRezervasyonlar.Count(x => x.GecikenCikisMi),
            ToplamRezervasyonSayisi = siralanmisRezervasyonlar.Count,
            ToplamKisiSayisi = siralanmisRezervasyonlar.Sum(x => x.KisiSayisi),
            ToplamKalanTutar = siralanmisRezervasyonlar.Sum(x => x.KalanTutar),
            ParaBirimi = siralanmisRezervasyonlar.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };

        return new GunlukGirisCikisRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Tarih = gun,
            ListeTipi = filtre,
            Baslik = $"{gun:dd.MM.yyyy} GÜNLÜK GİRİŞ-ÇIKIŞ LİSTESİ",
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
}
