using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.OrtalamaKonaklamaSuresi.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;

public class OrtalamaKonaklamaSuresiRaporService : IOrtalamaKonaklamaSuresiRaporService
{
    private const int MaksimumGunAraligi = 366;

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public OrtalamaKonaklamaSuresiRaporService(
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

    public async Task<OrtalamaKonaklamaSuresiRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis id.", 400);
        }

        if (baslangic.Date > bitis.Date)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
        }

        // Baslangic ve bitis tarihleri dahil sayilir.
        var toplamGunSayisiKontrol = (int)(bitis.Date - baslangic.Date).TotalDays + 1;
        if (toplamGunSayisiKontrol > MaksimumGunAraligi)
        {
            throw new BaseException($"Tarih araligi en fazla {MaksimumGunAraligi} gun olabilir.", 400);
        }

        if (odaTipiId.HasValue && odaTipiId.Value <= 0)
        {
            throw new BaseException("Gecersiz oda tipi id.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var baslangicGun = baslangic.Date;
        var bitisGun = bitis.Date;
        var bitisGunExclusive = bitisGun.AddDays(1);

        // OdaTipiAdi, tesis/aktif filtrelerinden gecen odalar uzerinden cozulur; boylece baska tesise
        // ait veya silinmis/pasif bir oda tipinin adi rapor basliginda gorunmez.
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

        var rezervasyonlar = await _stysDbContext.Rezervasyonlar
            .AsNoTracking()
            .Where(r => r.TesisId == tesisId
                && r.AktifMi
                && r.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && r.GirisTarihi < bitisGunExclusive
                && r.CikisTarihi > baslangicGun)
            .Select(r => new
            {
                r.Id,
                r.ReferansNo,
                r.MisafirAdiSoyadi,
                r.GirisTarihi,
                r.CikisTarihi,
                r.KisiSayisi,
                r.RezervasyonDurumu
            })
            .ToListAsync(cancellationToken);

        var rezervasyonIds = rezervasyonlar.Select(x => x.Id).ToList();

        var segmentOdaKayitlari = await _stysDbContext.RezervasyonSegmentleri
            .AsNoTracking()
            .Where(s => !s.IsDeleted
                && s.BaslangicTarihi < bitisGunExclusive
                && s.BitisTarihi > baslangicGun
                && rezervasyonIds.Contains(s.RezervasyonId))
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

        var filtrelenmisRezervasyonlar = new List<OrtalamaKonaklamaSuresiRezervasyonDto>();
        foreach (var r in rezervasyonlar)
        {
            if (odaTipiFiltresiUyanRezervasyonIds is not null && !odaTipiFiltresiUyanRezervasyonIds.Contains(r.Id))
            {
                continue;
            }

            // Cikis gunu konaklama gecesine dahil edilmez.
            var effectiveBaslangic = r.GirisTarihi.Date < baslangicGun ? baslangicGun : r.GirisTarihi.Date;
            var effectiveBitisExclusive = r.CikisTarihi.Date > bitisGunExclusive ? bitisGunExclusive : r.CikisTarihi.Date;
            var geceSayisi = Math.Max(0, (effectiveBitisExclusive - effectiveBaslangic).Days);

            if (geceSayisi == 0)
            {
                continue;
            }

            string konaklamaGrubu;
            string konaklamaGrubuLabel;
            if (geceSayisi <= 2)
            {
                konaklamaGrubu = "kisa";
                konaklamaGrubuLabel = "Kısa Konaklama";
            }
            else if (geceSayisi <= 7)
            {
                konaklamaGrubu = "orta";
                konaklamaGrubuLabel = "Orta Konaklama";
            }
            else
            {
                konaklamaGrubu = "uzun";
                konaklamaGrubuLabel = "Uzun Konaklama";
            }

            var segmentKayitlari = segmentOdaByRezervasyonId.GetValueOrDefault(r.Id, []);

            filtrelenmisRezervasyonlar.Add(new OrtalamaKonaklamaSuresiRezervasyonDto
            {
                RezervasyonId = r.Id,
                ReferansNo = r.ReferansNo,
                MisafirAdiSoyadi = r.MisafirAdiSoyadi,
                GirisTarihi = r.GirisTarihi,
                CikisTarihi = r.CikisTarihi,
                GeceSayisi = geceSayisi,
                KisiSayisi = r.KisiSayisi,
                OdaNolari = segmentKayitlari.Select(x => x.OdaNo).Distinct().OrderBy(x => x).ToList(),
                OdaTipleri = segmentKayitlari.Select(x => x.OdaTipiAdi).Distinct().OrderBy(x => x).ToList(),
                RezervasyonDurumu = r.RezervasyonDurumu,
                RezervasyonDurumuLabel = RezervasyonDurumuLabel(r.RezervasyonDurumu),
                KonaklamaGrubu = konaklamaGrubu,
                KonaklamaGrubuLabel = konaklamaGrubuLabel
            });
        }

        var siralanmisRezervasyonlar = filtrelenmisRezervasyonlar
            .OrderByDescending(x => x.GeceSayisi)
            .ThenBy(x => x.GirisTarihi)
            .ThenBy(x => x.MisafirAdiSoyadi)
            .ToList();

        // Ayni rezervasyon donem icinde farkli oda tiplerine gectiyse ilgili oda tipi kirilimlarinda
        // ayri ayri degerlendirilir (her oda tipi grubunda kendi gece/kisi sayisiyla sayilir).
        var odaTipiGruplari = siralanmisRezervasyonlar
            .SelectMany(r =>
            {
                var odaTipiIdleri = segmentOdaByRezervasyonId.GetValueOrDefault(r.RezervasyonId, [])
                    .Select(x => new { x.OdaTipiId, x.OdaTipiAdi })
                    .Distinct()
                    .ToList();

                return odaTipiIdleri.Select(ot => new { OdaTipi = ot, Rezervasyon = r });
            })
            .GroupBy(x => new { x.OdaTipi.OdaTipiId, x.OdaTipi.OdaTipiAdi })
            .Select(grup =>
            {
                var geceSayilari = grup.Select(x => x.Rezervasyon.GeceSayisi).ToList();
                var toplamGeceSayisi = geceSayilari.Sum();
                var rezervasyonSayisi = grup.Count();

                return new OrtalamaKonaklamaSuresiOdaTipiDto
                {
                    OdaTipiId = grup.Key.OdaTipiId,
                    OdaTipiAdi = grup.Key.OdaTipiAdi,
                    RezervasyonSayisi = rezervasyonSayisi,
                    ToplamKisiSayisi = grup.Sum(x => x.Rezervasyon.KisiSayisi),
                    ToplamGeceSayisi = toplamGeceSayisi,
                    OrtalamaGeceSayisi = rezervasyonSayisi == 0 ? 0m : Math.Round(toplamGeceSayisi / (decimal)rezervasyonSayisi, 2),
                    EnKisaKonaklamaGece = geceSayilari.Count == 0 ? 0 : geceSayilari.Min(),
                    EnUzunKonaklamaGece = geceSayilari.Count == 0 ? 0 : geceSayilari.Max()
                };
            })
            .OrderByDescending(x => x.OrtalamaGeceSayisi)
            .ThenBy(x => x.OdaTipiAdi)
            .ToList();

        var genelGeceSayilari = siralanmisRezervasyonlar.Select(x => x.GeceSayisi).ToList();
        var toplamRezervasyonSayisi = siralanmisRezervasyonlar.Count;
        var toplamGeceSayisiGenel = genelGeceSayilari.Sum();

        var ozet = new OrtalamaKonaklamaSuresiOzetDto
        {
            ToplamRezervasyonSayisi = toplamRezervasyonSayisi,
            ToplamKisiSayisi = siralanmisRezervasyonlar.Sum(x => x.KisiSayisi),
            ToplamGeceSayisi = toplamGeceSayisiGenel,
            OrtalamaGeceSayisi = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(toplamGeceSayisiGenel / (decimal)toplamRezervasyonSayisi, 2),
            EnKisaKonaklamaGece = genelGeceSayilari.Count == 0 ? 0 : genelGeceSayilari.Min(),
            EnUzunKonaklamaGece = genelGeceSayilari.Count == 0 ? 0 : genelGeceSayilari.Max(),
            KisaKonaklamaSayisi = siralanmisRezervasyonlar.Count(x => x.KonaklamaGrubu == "kisa"),
            OrtaKonaklamaSayisi = siralanmisRezervasyonlar.Count(x => x.KonaklamaGrubu == "orta"),
            UzunKonaklamaSayisi = siralanmisRezervasyonlar.Count(x => x.KonaklamaGrubu == "uzun")
        };

        return new OrtalamaKonaklamaSuresiRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Baslangic = baslangicGun,
            Bitis = bitisGun,
            OdaTipiId = odaTipiId,
            OdaTipiAdi = odaTipiAdi,
            Baslik = $"{baslangicGun:dd.MM.yyyy} - {bitisGun:dd.MM.yyyy} ORTALAMA KONAKLAMA SÜRESİ RAPORU",
            Ozet = ozet,
            OdaTipleri = odaTipiGruplari,
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
