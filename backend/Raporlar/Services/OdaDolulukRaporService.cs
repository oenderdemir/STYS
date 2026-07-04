using System.Globalization;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Licensing;
using STYS.Raporlar.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.Services;

public class OdaDolulukRaporService : IOdaDolulukRaporService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ILicenseService _licenseService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public OdaDolulukRaporService(
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        ILicenseService licenseService,
        ICurrentTenantAccessor currentTenantAccessor,
        IDomainOperationLogger domainLogger)
    {
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
        _licenseService = licenseService;
        _currentTenantAccessor = currentTenantAccessor;
        _domainLogger = domainLogger;
    }

    public async Task<AylikOdaDolulukRaporDto> GetAylikOdaDolulukRaporuAsync(
        int tesisId,
        int yil,
        int ay,
        bool maskele = false,
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

        if (yil < 2000 || yil > 2100)
        {
            throw new BaseException("Yil degeri 2000-2100 araliginda olmalidir.", 400);
        }

        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Rezervasyon, cancellationToken);

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var ayBaslangic = new DateTime(yil, ay, 1);
        var ayBitisExclusive = ayBaslangic.AddMonths(1);
        var ayBitis = ayBitisExclusive.AddDays(-1);

        var odalar = await _stysDbContext.Odalar
            .AsNoTracking()
            .Where(o => o.AktifMi
                && o.Bina != null && o.Bina.AktifMi && o.Bina.TesisId == tesisId
                && o.TesisOdaTipi != null && o.TesisOdaTipi.AktifMi)
            .OrderBy(o => o.Bina!.Ad)
            .ThenBy(o => o.OdaNo)
            .Select(o => new OdaDolulukOdaDto
            {
                OdaId = o.Id,
                OdaNo = o.OdaNo,
                BinaAdi = o.Bina!.Ad,
                OdaTipiAdi = o.TesisOdaTipi!.Ad,
                Kapasite = o.TesisOdaTipi.Kapasite
            })
            .ToListAsync(cancellationToken);

        var odaIds = odalar.Select(x => x.OdaId).ToHashSet();

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
                (s, a) => new SegmentOdaKaydi
                {
                    OdaId = a.OdaId,
                    AyrilanKisiSayisi = a.AyrilanKisiSayisi,
                    SegmentBaslangicTarihi = s.BaslangicTarihi,
                    SegmentBitisTarihi = s.BitisTarihi,
                    RezervasyonId = s.RezervasyonId,
                    ReferansNo = s.Rezervasyon!.ReferansNo,
                    MisafirAdiSoyadi = s.Rezervasyon.MisafirAdiSoyadi,
                    RezervasyonDurumu = s.Rezervasyon.RezervasyonDurumu,
                    ToplamUcret = s.Rezervasyon.ToplamUcret,
                    ParaBirimi = s.Rezervasyon.ParaBirimi
                })
            .ToListAsync(cancellationToken);

        var rezervasyonIds = segmentKayitlari.Select(x => x.RezervasyonId).Distinct().ToList();

        // A) Rezervasyon bazli toplam odeme: hucrelerdeki OdenenTutar/KalanTutar icin, odeme tarihinden bagimsiz.
        var odemeToplamlari = await _stysDbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(o => rezervasyonIds.Contains(o.RezervasyonId))
            .GroupBy(o => o.RezervasyonId)
            .Select(g => new { RezervasyonId = g.Key, Toplam = g.Sum(x => x.OdemeTutari) })
            .ToDictionaryAsync(x => x.RezervasyonId, x => x.Toplam, cancellationToken);

        // B) Ay icinde tahsil edilen odeme: odeme tarihi rapor ayi araliginda olan, iptal olmayan rezervasyonlarin odemeleri.
        var ayIcindeOdemeKayitlari = await _stysDbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(o => o.OdemeTarihi >= ayBaslangic
                && o.OdemeTarihi < ayBitisExclusive
                && o.Rezervasyon != null
                && o.Rezervasyon.TesisId == tesisId
                && o.Rezervasyon.AktifMi
                && o.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
            .Select(o => new
            {
                o.RezervasyonId,
                o.OdemeTarihi,
                o.OdemeTutari,
                o.ParaBirimi,
                o.OdemeTipi,
                o.Aciklama,
                ReferansNo = o.Rezervasyon!.ReferansNo,
                MisafirAdiSoyadi = o.Rezervasyon.MisafirAdiSoyadi
            })
            .ToListAsync(cancellationToken);

        var ayIcindeTahsilEdilenTutar = ayIcindeOdemeKayitlari.Sum(x => x.OdemeTutari);

        var hucreKayitlari = new Dictionary<(int OdaId, DateTime Gun), List<SegmentOdaKaydi>>();
        foreach (var kayit in segmentKayitlari)
        {
            var baslangic = kayit.SegmentBaslangicTarihi.Date < ayBaslangic ? ayBaslangic : kayit.SegmentBaslangicTarihi.Date;
            var bitisExclusive = kayit.SegmentBitisTarihi.Date > ayBitisExclusive ? ayBitisExclusive : kayit.SegmentBitisTarihi.Date;

            for (var gun = baslangic; gun < bitisExclusive; gun = gun.AddDays(1))
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

        var gunler = new List<OdaDolulukGunDto>();
        var doluOdaGunSayisi = 0;

        for (var gun = ayBaslangic; gun <= ayBitis; gun = gun.AddDays(1))
        {
            var gunDto = new OdaDolulukGunDto
            {
                Tarih = gun,
                GunAdi = ToTitleCase(gun.ToString("dddd", TrCulture))
            };

            foreach (var oda in odalar)
            {
                var hucre = new OdaDolulukHucreDto
                {
                    OdaId = oda.OdaId,
                    OdaNo = oda.OdaNo
                };

                if (hucreKayitlari.TryGetValue((oda.OdaId, gun), out var eslesenler) && eslesenler.Count > 0)
                {
                    var cakismaVarMi = eslesenler.Count > 1;
                    if (cakismaVarMi)
                    {
                        _domainLogger.Warning("Rapor.OdaDoluluk.OdaGunCakismasi", new
                        {
                            TesisId = tesisId,
                            OdaId = oda.OdaId,
                            Gun = gun,
                            RezervasyonIdleri = eslesenler.Select(x => x.RezervasyonId).ToList()
                        });
                    }

                    var kayit = eslesenler[0];
                    var odenenTutar = odemeToplamlari.GetValueOrDefault(kayit.RezervasyonId);
                    var kalanTutar = kayit.ToplamUcret - odenenTutar;
                    var odemesiEksikMi = kalanTutar > 0m;

                    hucre.DoluMu = true;
                    hucre.RezervasyonId = kayit.RezervasyonId;
                    hucre.ReferansNo = kayit.ReferansNo;
                    hucre.MisafirAdiSoyadi = maskele ? MaskeleAdSoyad(kayit.MisafirAdiSoyadi) : kayit.MisafirAdiSoyadi;
                    // TODO: Rezervasyon domaininde kurum/unite alani bulunmuyor; ileride eklendiginde burada doldurulmali.
                    hucre.KurumUnite = null;
                    hucre.KisiSayisi = kayit.AyrilanKisiSayisi;
                    hucre.GirisTarihi = kayit.SegmentBaslangicTarihi;
                    hucre.CikisTarihi = kayit.SegmentBitisTarihi;
                    hucre.RezervasyonDurumu = kayit.RezervasyonDurumu;
                    hucre.ToplamUcret = kayit.ToplamUcret;
                    hucre.OdenenTutar = odenenTutar;
                    hucre.KalanTutar = kalanTutar;
                    hucre.ParaBirimi = kayit.ParaBirimi;
                    hucre.OdemesiEksikMi = odemesiEksikMi;
                    hucre.OdaDegisimiGerekliMi = false;
                    hucre.TutarAciklamasi = "Tutar bilgisi rezervasyon toplamıdır.";
                    hucre.HucreRenkKodu = cakismaVarMi
                        ? "conflict"
                        : odemesiEksikMi
                            ? "payment-missing"
                            : kayit.RezervasyonDurumu switch
                            {
                                RezervasyonDurumlari.CheckOutTamamlandi => "checked-out",
                                RezervasyonDurumlari.CheckInTamamlandi => "occupied",
                                _ => "reserved"
                            };

                    hucre.CakismaVarMi = cakismaVarMi;
                    hucre.CakismaSayisi = eslesenler.Count;
                    if (cakismaVarMi)
                    {
                        hucre.Cakismalar = eslesenler
                            .Select(x => new OdaDolulukCakismaDto
                            {
                                RezervasyonId = x.RezervasyonId,
                                ReferansNo = x.ReferansNo,
                                MisafirAdiSoyadi = maskele ? MaskeleAdSoyad(x.MisafirAdiSoyadi) : x.MisafirAdiSoyadi,
                                GirisTarihi = x.SegmentBaslangicTarihi,
                                CikisTarihi = x.SegmentBitisTarihi,
                                RezervasyonDurumu = x.RezervasyonDurumu
                            })
                            .ToList();
                    }

                    doluOdaGunSayisi++;
                }

                gunDto.Hucreler.Add(hucre);
            }

            gunler.Add(gunDto);
        }

        var benzersizRezervasyonlar = segmentKayitlari
            .GroupBy(x => x.RezervasyonId)
            .Select(g => g.First())
            .ToList();

        var odaNoById = odalar.ToDictionary(x => x.OdaId, x => x.OdaNo);
        var rezervasyonBilgisiById = benzersizRezervasyonlar.ToDictionary(x => x.RezervasyonId);

        var tahsilatlar = ayIcindeOdemeKayitlari
            .OrderBy(x => x.OdemeTarihi)
            .Select(o =>
            {
                rezervasyonBilgisiById.TryGetValue(o.RezervasyonId, out var bilgi);
                var misafirAdi = maskele ? MaskeleAdSoyad(o.MisafirAdiSoyadi) : o.MisafirAdiSoyadi;

                return new OdaDolulukTahsilatDto
                {
                    RezervasyonId = o.RezervasyonId,
                    OdaId = bilgi?.OdaId,
                    OdaNo = bilgi is not null ? odaNoById.GetValueOrDefault(bilgi.OdaId) : null,
                    OdemeTarihi = o.OdemeTarihi,
                    OdemeTutari = o.OdemeTutari,
                    ParaBirimi = o.ParaBirimi,
                    OdemeTipi = o.OdemeTipi,
                    Aciklama = o.Aciklama,
                    MisafirAdiSoyadi = misafirAdi,
                    // TODO: Rezervasyon domaininde kurum/unite alani bulunmuyor; ileride eklendiginde burada doldurulmali.
                    KurumUnite = null,
                    ReferansNo = o.ReferansNo,
                    GirisTarihi = bilgi?.SegmentBaslangicTarihi,
                    CikisTarihi = bilgi?.SegmentBitisTarihi,
                    MakbuzNo = null,
                    OdemeYapan = misafirAdi
                };
            })
            .ToList();

        var toplamOdaGunSayisi = odalar.Count * gunler.Count;
        var bosOdaGunSayisi = toplamOdaGunSayisi - doluOdaGunSayisi;

        var konaklayanRezervasyonlarinToplamTahsilati = benzersizRezervasyonlar
            .Sum(x => odemeToplamlari.GetValueOrDefault(x.RezervasyonId));
        var konaklayanRezervasyonlarinToplamKalanTutari = benzersizRezervasyonlar
            .Sum(x => Math.Max(0m, x.ToplamUcret - odemeToplamlari.GetValueOrDefault(x.RezervasyonId)));

        var ozet = new OdaDolulukOzetDto
        {
            ToplamOdaSayisi = odalar.Count,
            GunSayisi = gunler.Count,
            ToplamOdaGunSayisi = toplamOdaGunSayisi,
            DoluOdaGunSayisi = doluOdaGunSayisi,
            BosOdaGunSayisi = bosOdaGunSayisi,
            DolulukOraniYuzde = toplamOdaGunSayisi > 0
                ? Math.Round(doluOdaGunSayisi * 100m / toplamOdaGunSayisi, 2)
                : 0m,
            AyIcindeTahsilEdilenTutar = ayIcindeTahsilEdilenTutar,
            KonaklayanRezervasyonlarinToplamTahsilati = konaklayanRezervasyonlarinToplamTahsilati,
            KonaklayanRezervasyonlarinToplamKalanTutari = konaklayanRezervasyonlarinToplamKalanTutari,
            // Geriye uyumluluk: ToplamTahsilat artik ay icinde tahsil edileni, ToplamKalanTutar konaklayan rezervasyonlarin kalanini gosterir.
            ToplamTahsilat = ayIcindeTahsilEdilenTutar,
            ToplamKalanTutar = konaklayanRezervasyonlarinToplamKalanTutari
        };

        return new AylikOdaDolulukRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Yil = yil,
            Ay = ay,
            BaslangicTarihi = ayBaslangic,
            BitisTarihi = ayBitis,
            Odalar = odalar,
            Gunler = gunler,
            Ozet = ozet,
            Tahsilatlar = tahsilatlar
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

    private static string ToTitleCase(string value)
    {
        return value.Length == 0 ? value : TrCulture.TextInfo.ToTitleCase(value);
    }

    private static string MaskeleAdSoyad(string adSoyad)
    {
        if (string.IsNullOrWhiteSpace(adSoyad))
        {
            return adSoyad;
        }

        var parcalar = adSoyad.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parcalar.Select(p => p.Length <= 1 ? p : p[0] + new string('*', p.Length - 1)));
    }

    private sealed class SegmentOdaKaydi
    {
        public int OdaId { get; set; }
        public int AyrilanKisiSayisi { get; set; }
        public DateTime SegmentBaslangicTarihi { get; set; }
        public DateTime SegmentBitisTarihi { get; set; }
        public int RezervasyonId { get; set; }
        public string ReferansNo { get; set; } = string.Empty;
        public string MisafirAdiSoyadi { get; set; } = string.Empty;
        public string RezervasyonDurumu { get; set; } = string.Empty;
        public decimal ToplamUcret { get; set; }
        public string ParaBirimi { get; set; } = string.Empty;
    }
}
