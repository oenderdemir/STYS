using System.Globalization;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.OdaMusaitlik.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.OdaMusaitlik.Services;

public class OdaMusaitlikRaporService : IOdaMusaitlikRaporService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");
    private const int MaksimumGunAraligi = 60;

    private static readonly HashSet<string> GecerliDurumlar =
    [
        "tumu", "tamamen-bos", "tamamen-dolu", "kismen-musait"
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public OdaMusaitlikRaporService(
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

    public async Task<OdaMusaitlikRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? durum = null,
        int? odaTipiId = null,
        int? kapasite = null,
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

        // Baslangic ve bitis tarihleri dahil sayilir; ornegin 01.07-07.07 araligi 7 (dahil) gun demektir.
        var toplamGunSayisiKontrol = (int)(bitis.Date - baslangic.Date).TotalDays + 1;
        if (toplamGunSayisiKontrol > MaksimumGunAraligi)
        {
            throw new BaseException($"Tarih araligi en fazla {MaksimumGunAraligi} gun olabilir.", 400);
        }

        var filtre = string.IsNullOrWhiteSpace(durum) ? "tumu" : durum.Trim().ToLowerInvariant();
        if (!GecerliDurumlar.Contains(filtre))
        {
            throw new BaseException("Gecersiz durum filtresi.", 400);
        }

        if (odaTipiId.HasValue && odaTipiId.Value <= 0)
        {
            throw new BaseException("Gecersiz oda tipi id.", 400);
        }

        if (kapasite.HasValue && kapasite.Value <= 0)
        {
            throw new BaseException("Gecersiz kapasite degeri.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        string? odaTipiAdi = null;
        if (odaTipiId.HasValue)
        {
            odaTipiAdi = await _stysDbContext.OdaTipleri
                .AsNoTracking()
                .Where(x => x.Id == odaTipiId.Value)
                .Select(x => x.Ad)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var baslangicGun = baslangic.Date;
        var bitisGun = bitis.Date;
        var bitisGunExclusive = bitisGun.AddDays(1);
        var toplamGunSayisi = toplamGunSayisiKontrol;

        var odaQuery = _stysDbContext.Odalar
            .AsNoTracking()
            .Where(o => !o.IsDeleted
                && o.AktifMi
                && o.Bina != null && !o.Bina.IsDeleted && o.Bina.AktifMi && o.Bina.TesisId == tesisId
                && o.TesisOdaTipi != null && !o.TesisOdaTipi.IsDeleted && o.TesisOdaTipi.AktifMi);

        if (odaTipiId.HasValue)
        {
            odaQuery = odaQuery.Where(o => o.TesisOdaTipiId == odaTipiId.Value);
        }

        if (kapasite.HasValue)
        {
            odaQuery = odaQuery.Where(o => o.TesisOdaTipi!.Kapasite >= kapasite.Value);
        }

        var odalar = await odaQuery
            .OrderBy(o => o.Bina!.Ad)
            .ThenBy(o => o.OdaNo)
            .Select(o => new
            {
                OdaId = o.Id,
                o.OdaNo,
                BinaAdi = o.Bina!.Ad,
                OdaTipiAdi = o.TesisOdaTipi!.Ad,
                Kapasite = o.TesisOdaTipi.Kapasite
            })
            .ToListAsync(cancellationToken);

        var odaIds = odalar.Select(x => x.OdaId).ToHashSet();

        var segmentKayitlari = await _stysDbContext.RezervasyonSegmentleri
            .AsNoTracking()
            .Where(s => !s.IsDeleted
                && s.BaslangicTarihi < bitisGunExclusive
                && s.BitisTarihi > baslangicGun
                && s.Rezervasyon != null
                && !s.Rezervasyon.IsDeleted
                && s.Rezervasyon.TesisId == tesisId
                && s.Rezervasyon.AktifMi
                && s.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
            .SelectMany(
                s => s.OdaAtamalari.Where(a => !a.IsDeleted && odaIds.Contains(a.OdaId)),
                (s, a) => new SegmentOdaKaydi
                {
                    OdaId = a.OdaId,
                    SegmentBaslangicTarihi = s.BaslangicTarihi,
                    SegmentBitisTarihi = s.BitisTarihi,
                    RezervasyonId = s.RezervasyonId,
                    ReferansNo = s.Rezervasyon!.ReferansNo,
                    MisafirAdiSoyadi = s.Rezervasyon.MisafirAdiSoyadi,
                    RezervasyonDurumu = s.Rezervasyon.RezervasyonDurumu
                })
            .ToListAsync(cancellationToken);

        // Cikis gunu oda tekrar kullanilabilir kabul edildigi icin dolu gun hesabina dahil edilmez.
        var hucreKayitlari = new Dictionary<(int OdaId, DateTime Gun), List<SegmentOdaKaydi>>();
        foreach (var kayit in segmentKayitlari)
        {
            var baslangicSiniri = kayit.SegmentBaslangicTarihi.Date < baslangicGun ? baslangicGun : kayit.SegmentBaslangicTarihi.Date;
            var bitisSiniriExclusive = kayit.SegmentBitisTarihi.Date > bitisGunExclusive ? bitisGunExclusive : kayit.SegmentBitisTarihi.Date;

            for (var gun = baslangicSiniri; gun < bitisSiniriExclusive; gun = gun.AddDays(1))
            {
                var anahtar = (kayit.OdaId, gun);
                if (!hucreKayitlari.TryGetValue(anahtar, out var liste))
                {
                    liste = [];
                    hucreKayitlari[anahtar] = liste;
                }

                liste.Add(kayit);
            }
        }

        var tumOdalar = odalar.Select(oda =>
        {
            var gunler = new List<OdaMusaitlikGunDto>();
            var bosGunSayisi = 0;
            var doluGunSayisi = 0;

            for (var gun = baslangicGun; gun <= bitisGun; gun = gun.AddDays(1))
            {
                var gunDto = new OdaMusaitlikGunDto
                {
                    Tarih = gun,
                    GunAdi = ToTitleCase(gun.ToString("dddd", TrCulture))
                };

                if (hucreKayitlari.TryGetValue((oda.OdaId, gun), out var eslesenler) && eslesenler.Count > 0)
                {
                    // Ayni oda/gun icin birden fazla rezervasyon bulunursa ileride cakisma detaylari gosterilebilir.
                    var kayit = eslesenler[0];
                    gunDto.DoluMu = true;
                    gunDto.BosMu = false;
                    gunDto.RezervasyonId = kayit.RezervasyonId;
                    gunDto.ReferansNo = kayit.ReferansNo;
                    gunDto.MisafirAdiSoyadi = kayit.MisafirAdiSoyadi;
                    gunDto.RezervasyonDurumu = kayit.RezervasyonDurumu;
                    gunDto.RezervasyonDurumuLabel = RezervasyonDurumuLabel(kayit.RezervasyonDurumu);
                    doluGunSayisi++;
                }
                else
                {
                    gunDto.BosMu = true;
                    gunDto.DoluMu = false;
                    bosGunSayisi++;
                }

                gunler.Add(gunDto);
            }

            string musaitlikDurumu;
            string musaitlikDurumuLabel;
            if (bosGunSayisi == gunler.Count)
            {
                musaitlikDurumu = "tamamen-bos";
                musaitlikDurumuLabel = "Tamamen Boş";
            }
            else if (doluGunSayisi == gunler.Count)
            {
                musaitlikDurumu = "tamamen-dolu";
                musaitlikDurumuLabel = "Tamamen Dolu";
            }
            else
            {
                musaitlikDurumu = "kismen-musait";
                musaitlikDurumuLabel = "Kısmen Müsait";
            }

            return new OdaMusaitlikOdaDto
            {
                OdaId = oda.OdaId,
                OdaNo = oda.OdaNo,
                BinaAdi = oda.BinaAdi,
                OdaTipiAdi = oda.OdaTipiAdi,
                Kapasite = oda.Kapasite,
                MusaitlikDurumu = musaitlikDurumu,
                MusaitlikDurumuLabel = musaitlikDurumuLabel,
                ToplamGunSayisi = gunler.Count,
                BosGunSayisi = bosGunSayisi,
                DoluGunSayisi = doluGunSayisi,
                MusaitlikOrani = gunler.Count == 0 ? 0m : Math.Round(bosGunSayisi * 100m / gunler.Count, 2),
                Gunler = gunler
            };
        }).ToList();

        var filtrelenmisOdalar = filtre switch
        {
            "tamamen-bos" => tumOdalar.Where(x => x.MusaitlikDurumu == "tamamen-bos").ToList(),
            "tamamen-dolu" => tumOdalar.Where(x => x.MusaitlikDurumu == "tamamen-dolu").ToList(),
            "kismen-musait" => tumOdalar.Where(x => x.MusaitlikDurumu == "kismen-musait").ToList(),
            _ => tumOdalar
        };

        var toplamOdaGunSayisi = filtrelenmisOdalar.Sum(x => x.ToplamGunSayisi);
        var bosOdaGunSayisiToplam = filtrelenmisOdalar.Sum(x => x.BosGunSayisi);
        var doluOdaGunSayisiToplam = filtrelenmisOdalar.Sum(x => x.DoluGunSayisi);

        var ozet = new OdaMusaitlikOzetDto
        {
            ToplamOdaSayisi = filtrelenmisOdalar.Count,
            TamamenBosOdaSayisi = filtrelenmisOdalar.Count(x => x.MusaitlikDurumu == "tamamen-bos"),
            TamamenDoluOdaSayisi = filtrelenmisOdalar.Count(x => x.MusaitlikDurumu == "tamamen-dolu"),
            KismenMusaitOdaSayisi = filtrelenmisOdalar.Count(x => x.MusaitlikDurumu == "kismen-musait"),
            ToplamGunSayisi = toplamGunSayisi,
            ToplamOdaGunSayisi = toplamOdaGunSayisi,
            BosOdaGunSayisi = bosOdaGunSayisiToplam,
            DoluOdaGunSayisi = doluOdaGunSayisiToplam,
            MusaitlikOrani = toplamOdaGunSayisi == 0 ? 0m : Math.Round(bosOdaGunSayisiToplam * 100m / toplamOdaGunSayisi, 2)
        };

        return new OdaMusaitlikRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Baslangic = baslangicGun,
            Bitis = bitisGun,
            Durum = filtre,
            OdaTipiId = odaTipiId,
            OdaTipiAdi = odaTipiAdi,
            Kapasite = kapasite,
            Baslik = $"{baslangicGun:dd.MM.yyyy} - {bitisGun:dd.MM.yyyy} BOŞ ODA / MÜSAİTLİK RAPORU",
            Ozet = ozet,
            Odalar = filtrelenmisOdalar
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

    private static string ToTitleCase(string value)
    {
        return value.Length == 0 ? value : TrCulture.TextInfo.ToTitleCase(value);
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

    private sealed class SegmentOdaKaydi
    {
        public int OdaId { get; set; }
        public DateTime SegmentBaslangicTarihi { get; set; }
        public DateTime SegmentBitisTarihi { get; set; }
        public int RezervasyonId { get; set; }
        public string ReferansNo { get; set; } = string.Empty;
        public string MisafirAdiSoyadi { get; set; } = string.Empty;
        public string RezervasyonDurumu { get; set; } = string.Empty;
    }
}
