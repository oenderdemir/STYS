using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.OdaTipiDoluluk.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.OdaTipiDoluluk.Services;

public class OdaTipiDolulukRaporService : IOdaTipiDolulukRaporService
{
    private const int MaksimumGunAraligi = 366;

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public OdaTipiDolulukRaporService(
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

    public async Task<OdaTipiDolulukRaporDto> GetRaporAsync(
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

        // Baslangic ve bitis tarihleri dahil sayilir; ornegin 01.07-31.07 araligi 31 (dahil) gun demektir.
        var toplamGunSayisi = (int)(bitis.Date - baslangic.Date).TotalDays + 1;
        if (toplamGunSayisi > MaksimumGunAraligi)
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

        var odalar = await odaQuery
            .OrderBy(o => o.Bina!.Ad)
            .ThenBy(o => o.OdaNo)
            .Select(o => new
            {
                OdaId = o.Id,
                o.OdaNo,
                BinaAdi = o.Bina!.Ad,
                OdaTipiId = o.TesisOdaTipiId,
                OdaTipiAdi = o.TesisOdaTipi!.Ad,
                Kapasite = o.TesisOdaTipi.Kapasite
            })
            .ToListAsync(cancellationToken);

        // OdaTipiAdi, erisim/tesis/aktif filtrelerinden gecen odalar sonucundan cozulur; boylece baska
        // tesise ait veya silinmis/pasif bir oda tipinin adi rapor basliginda gorunmez. Bu tesiste ilgili
        // oda tipine ait aktif oda yoksa null kalir (Excel'de "ID: {odaTipiId}" fallback'i devam eder).
        var odaTipiAdi = odaTipiId.HasValue ? odalar.Select(x => x.OdaTipiAdi).FirstOrDefault() : null;

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
                    KisiSayisi = s.Rezervasyon!.KisiSayisi
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

        var odaDetaylari = odalar.Select(oda =>
        {
            var doluGunSayisi = 0;
            for (var gun = baslangicGun; gun <= bitisGun; gun = gun.AddDays(1))
            {
                // Ayni oda/gun icin birden fazla rezervasyon bulunursa cakisma raporunda ayrica ele alinabilir; burada yine 1 dolu oda/gun sayilir.
                if (hucreKayitlari.TryGetValue((oda.OdaId, gun), out var eslesenler) && eslesenler.Count > 0)
                {
                    doluGunSayisi++;
                }
            }

            var bosGunSayisi = toplamGunSayisi - doluGunSayisi;

            return new
            {
                oda.OdaId,
                oda.OdaNo,
                oda.BinaAdi,
                oda.OdaTipiId,
                oda.OdaTipiAdi,
                oda.Kapasite,
                DoluGunSayisi = doluGunSayisi,
                BosGunSayisi = bosGunSayisi
            };
        }).ToList();

        var odaTipiGruplari = odaDetaylari
            .GroupBy(x => new { x.OdaTipiId, x.OdaTipiAdi })
            .Select(grup =>
            {
                var odaIdleriBuOdaTipinde = grup.Select(x => x.OdaId).ToHashSet();

                var odaSayisi = grup.Count();
                var toplamKapasite = grup.Sum(x => x.Kapasite);
                var toplamOdaGunSayisi = odaSayisi * toplamGunSayisi;
                var doluOdaGunSayisi = grup.Sum(x => x.DoluGunSayisi);
                var bosOdaGunSayisi = toplamOdaGunSayisi - doluOdaGunSayisi;

                var buOdaTipindekiKayitlar = segmentKayitlari.Where(x => odaIdleriBuOdaTipinde.Contains(x.OdaId)).ToList();

                var benzersizRezervasyonlar = buOdaTipindekiKayitlar
                    .GroupBy(x => x.RezervasyonId)
                    .Select(g => g.First())
                    .ToList();

                // Kisi-gece hesabi oda tipi kullanim yogunlugu icin yaklasik metrik olarak hesaplanir;
                // ayni rezervasyon ayni gece birden fazla odaya atanmissa kisi sayisi tekrar sayilabilir.
                var toplamKisiGeceSayisi = 0;
                foreach (var kayit in buOdaTipindekiKayitlar)
                {
                    var effectiveBaslangic = kayit.SegmentBaslangicTarihi.Date < baslangicGun ? baslangicGun : kayit.SegmentBaslangicTarihi.Date;
                    var effectiveBitisExclusive = kayit.SegmentBitisTarihi.Date > bitisGunExclusive ? bitisGunExclusive : kayit.SegmentBitisTarihi.Date;
                    var geceSayisi = Math.Max(0, (effectiveBitisExclusive - effectiveBaslangic).Days);
                    toplamKisiGeceSayisi += geceSayisi * kayit.KisiSayisi;
                }

                var odalarListesi = grup
                    .OrderBy(x => x.BinaAdi)
                    .ThenBy(x => x.OdaNo)
                    .Select(x => new OdaTipiDolulukOdaDto
                    {
                        OdaId = x.OdaId,
                        OdaNo = x.OdaNo,
                        BinaAdi = x.BinaAdi,
                        Kapasite = x.Kapasite,
                        ToplamGunSayisi = toplamGunSayisi,
                        DoluGunSayisi = x.DoluGunSayisi,
                        BosGunSayisi = x.BosGunSayisi,
                        DolulukOrani = toplamGunSayisi == 0 ? 0m : Math.Round(x.DoluGunSayisi * 100m / toplamGunSayisi, 2)
                    })
                    .ToList();

                return new OdaTipiDolulukSatirDto
                {
                    OdaTipiId = grup.Key.OdaTipiId,
                    OdaTipiAdi = grup.Key.OdaTipiAdi,
                    OdaSayisi = odaSayisi,
                    ToplamKapasite = toplamKapasite,
                    ToplamGunSayisi = toplamGunSayisi,
                    ToplamOdaGunSayisi = toplamOdaGunSayisi,
                    DoluOdaGunSayisi = doluOdaGunSayisi,
                    BosOdaGunSayisi = bosOdaGunSayisi,
                    DolulukOrani = toplamOdaGunSayisi == 0 ? 0m : Math.Round(doluOdaGunSayisi * 100m / toplamOdaGunSayisi, 2),
                    MusaitlikOrani = toplamOdaGunSayisi == 0 ? 100m : Math.Round(bosOdaGunSayisi * 100m / toplamOdaGunSayisi, 2),
                    ToplamRezervasyonSayisi = benzersizRezervasyonlar.Count,
                    ToplamKonaklayanKisiSayisi = benzersizRezervasyonlar.Sum(x => x.KisiSayisi),
                    ToplamKisiGeceSayisi = toplamKisiGeceSayisi,
                    Odalar = odalarListesi
                };
            })
            .OrderByDescending(x => x.DolulukOrani)
            .ThenBy(x => x.OdaTipiAdi)
            .ToList();

        var toplamOdaGunSayisiOzet = odaTipiGruplari.Sum(x => x.ToplamOdaGunSayisi);
        var doluOdaGunSayisiOzet = odaTipiGruplari.Sum(x => x.DoluOdaGunSayisi);
        var bosOdaGunSayisiOzet = odaTipiGruplari.Sum(x => x.BosOdaGunSayisi);

        var ozet = new OdaTipiDolulukOzetDto
        {
            ToplamOdaTipiSayisi = odaTipiGruplari.Count,
            ToplamOdaSayisi = odaTipiGruplari.Sum(x => x.OdaSayisi),
            ToplamKapasite = odaTipiGruplari.Sum(x => x.ToplamKapasite),
            ToplamGunSayisi = toplamGunSayisi,
            ToplamOdaGunSayisi = toplamOdaGunSayisiOzet,
            DoluOdaGunSayisi = doluOdaGunSayisiOzet,
            BosOdaGunSayisi = bosOdaGunSayisiOzet,
            DolulukOrani = toplamOdaGunSayisiOzet == 0 ? 0m : Math.Round(doluOdaGunSayisiOzet * 100m / toplamOdaGunSayisiOzet, 2),
            MusaitlikOrani = toplamOdaGunSayisiOzet == 0 ? 100m : Math.Round(bosOdaGunSayisiOzet * 100m / toplamOdaGunSayisiOzet, 2)
        };

        return new OdaTipiDolulukRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Baslangic = baslangicGun,
            Bitis = bitisGun,
            OdaTipiId = odaTipiId,
            OdaTipiAdi = odaTipiAdi,
            Baslik = $"{baslangicGun:dd.MM.yyyy} - {bitisGun:dd.MM.yyyy} ODA TİPİ BAZLI DOLULUK RAPORU",
            Ozet = ozet,
            OdaTipleri = odaTipiGruplari
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

    private sealed class SegmentOdaKaydi
    {
        public int OdaId { get; set; }
        public DateTime SegmentBaslangicTarihi { get; set; }
        public DateTime SegmentBitisTarihi { get; set; }
        public int RezervasyonId { get; set; }
        public int KisiSayisi { get; set; }
    }
}
