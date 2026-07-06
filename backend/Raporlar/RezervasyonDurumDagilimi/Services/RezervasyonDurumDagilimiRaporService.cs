using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.RezervasyonDurumDagilimi.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.RezervasyonDurumDagilimi.Services;

public class RezervasyonDurumDagilimiRaporService : IRezervasyonDurumDagilimiRaporService
{
    private const int MaksimumGunAraligi = 366;

    private static readonly Dictionary<string, string> DurumFiltreEslemeleri = new()
    {
        ["taslak"] = RezervasyonDurumlari.Taslak,
        ["onayli"] = RezervasyonDurumlari.Onayli,
        ["check-in-tamamlandi"] = RezervasyonDurumlari.CheckInTamamlandi,
        ["check-out-tamamlandi"] = RezervasyonDurumlari.CheckOutTamamlandi,
        ["iptal"] = RezervasyonDurumlari.Iptal
    };

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public RezervasyonDurumDagilimiRaporService(
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

    public async Task<RezervasyonDurumDagilimiRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        string? durum = null,
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

        string? durumFiltre = null;
        string? durumConstant = null;
        if (!string.IsNullOrWhiteSpace(durum))
        {
            durumFiltre = durum.Trim().ToLowerInvariant();
            if (!DurumFiltreEslemeleri.TryGetValue(durumFiltre, out durumConstant))
            {
                throw new BaseException("Gecersiz durum filtresi.", 400);
            }
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

        // Bu raporun amaci durum dagilimidir; iptal rezervasyonlar (soft-delete ve aktif olmayanlarin
        // aksine) bilinclli olarak hric tutulmaz, cunku iptal sayisi/orani raporun konusudur.
        var rezervasyonlar = await _stysDbContext.Rezervasyonlar
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.TesisId == tesisId
                && r.AktifMi
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

        var calisilmisRezervasyonlar = new List<CalisilmisRezervasyon>();
        foreach (var r in rezervasyonlar)
        {
            if (odaTipiFiltresiUyanRezervasyonIds is not null && !odaTipiFiltresiUyanRezervasyonIds.Contains(r.Id))
            {
                continue;
            }

            if (durumConstant is not null && r.RezervasyonDurumu != durumConstant)
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

            var segmentKayitlari = segmentOdaByRezervasyonId.GetValueOrDefault(r.Id, []);
            if (odaTipiId.HasValue)
            {
                segmentKayitlari = segmentKayitlari
                    .Where(x => x.OdaTipiId == odaTipiId.Value)
                    .ToList();
            }

            calisilmisRezervasyonlar.Add(new CalisilmisRezervasyon
            {
                RezervasyonId = r.Id,
                ReferansNo = r.ReferansNo,
                MisafirAdiSoyadi = r.MisafirAdiSoyadi,
                GirisTarihi = r.GirisTarihi,
                CikisTarihi = r.CikisTarihi,
                GeceSayisi = geceSayisi,
                KisiSayisi = r.KisiSayisi,
                RezervasyonDurumu = r.RezervasyonDurumu,
                SegmentKayitlari = segmentKayitlari
            });
        }

        var siralanmisRezervasyonlar = calisilmisRezervasyonlar
            .Select(x => new RezervasyonDurumDagilimiRezervasyonDto
            {
                RezervasyonId = x.RezervasyonId,
                ReferansNo = x.ReferansNo,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi,
                GeceSayisi = x.GeceSayisi,
                KisiSayisi = x.KisiSayisi,
                RezervasyonDurumu = x.RezervasyonDurumu,
                RezervasyonDurumuLabel = RezervasyonDurumuLabel(x.RezervasyonDurumu),
                OdaNolari = x.SegmentKayitlari.Select(s => s.OdaNo).Distinct().OrderBy(s => s).ToList(),
                OdaTipleri = x.SegmentKayitlari.Select(s => s.OdaTipiAdi).Distinct().OrderBy(s => s).ToList()
            })
            .OrderBy(x => x.GirisTarihi)
            .ThenBy(x => x.RezervasyonDurumuLabel)
            .ThenBy(x => x.MisafirAdiSoyadi)
            .ToList();

        var toplamRezervasyonSayisi = calisilmisRezervasyonlar.Count;

        var durumlar = calisilmisRezervasyonlar
            .GroupBy(x => x.RezervasyonDurumu)
            .Select(grup =>
            {
                var rezervasyonSayisi = grup.Count();
                return new RezervasyonDurumDagilimiDurumSatiriDto
                {
                    Durum = grup.Key,
                    DurumLabel = RezervasyonDurumuLabel(grup.Key),
                    RezervasyonSayisi = rezervasyonSayisi,
                    KisiSayisi = grup.Sum(x => x.KisiSayisi),
                    GeceSayisi = grup.Sum(x => x.GeceSayisi),
                    Oran = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(rezervasyonSayisi * 100m / toplamRezervasyonSayisi, 2)
                };
            })
            .OrderByDescending(x => x.RezervasyonSayisi)
            .ThenBy(x => x.DurumLabel)
            .ToList();

        // Ayni rezervasyon donem icinde farkli oda tiplerine gectiyse ilgili oda tipi kirilimlarinda
        // ayri ayri degerlendirilir (her oda tipi grubunda kendi gece/kisi/durum bilgisiyle sayilir).
        var odaTipleri = calisilmisRezervasyonlar
            .SelectMany(r =>
            {
                var odaTipiIdleri = r.SegmentKayitlari
                    .Select(x => new { x.OdaTipiId, x.OdaTipiAdi })
                    .Distinct()
                    .ToList();

                return odaTipiIdleri.Select(ot => new { OdaTipi = ot, Rezervasyon = r });
            })
            .GroupBy(x => new { x.OdaTipi.OdaTipiId, x.OdaTipi.OdaTipiAdi })
            .Select(grup =>
            {
                var rezervasyonSayisi = grup.Count();
                var iptalSayisi = grup.Count(x => x.Rezervasyon.RezervasyonDurumu == RezervasyonDurumlari.Iptal);
                var gerceklesenSayisi = grup.Count(x => x.Rezervasyon.RezervasyonDurumu is RezervasyonDurumlari.CheckInTamamlandi or RezervasyonDurumlari.CheckOutTamamlandi);

                return new RezervasyonDurumDagilimiOdaTipiSatiriDto
                {
                    OdaTipiId = grup.Key.OdaTipiId,
                    OdaTipiAdi = grup.Key.OdaTipiAdi,
                    RezervasyonSayisi = rezervasyonSayisi,
                    IptalSayisi = iptalSayisi,
                    GerceklesenSayisi = gerceklesenSayisi,
                    KisiSayisi = grup.Sum(x => x.Rezervasyon.KisiSayisi),
                    GeceSayisi = grup.Sum(x => x.Rezervasyon.GeceSayisi),
                    IptalOrani = rezervasyonSayisi == 0 ? 0m : Math.Round(iptalSayisi * 100m / rezervasyonSayisi, 2),
                    GerceklesmeOrani = rezervasyonSayisi == 0 ? 0m : Math.Round(gerceklesenSayisi * 100m / rezervasyonSayisi, 2)
                };
            })
            .OrderByDescending(x => x.RezervasyonSayisi)
            .ThenBy(x => x.OdaTipiAdi)
            .ToList();

        var taslakSayisi = calisilmisRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.Taslak);
        var onayliSayisi = calisilmisRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.Onayli);
        var checkInSayisi = calisilmisRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.CheckInTamamlandi);
        var checkOutSayisi = calisilmisRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi);
        var iptalSayisiGenel = calisilmisRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.Iptal);
        var gerceklesenSayisiGenel = checkInSayisi + checkOutSayisi;

        var ozet = new RezervasyonDurumDagilimiOzetDto
        {
            ToplamRezervasyonSayisi = toplamRezervasyonSayisi,
            TaslakSayisi = taslakSayisi,
            OnayliSayisi = onayliSayisi,
            CheckInTamamlandiSayisi = checkInSayisi,
            CheckOutTamamlandiSayisi = checkOutSayisi,
            IptalSayisi = iptalSayisiGenel,
            GerceklesenRezervasyonSayisi = gerceklesenSayisiGenel,
            DevamEdenRezervasyonSayisi = checkInSayisi,
            IptalOrani = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(iptalSayisiGenel * 100m / toplamRezervasyonSayisi, 2),
            GerceklesmeOrani = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(gerceklesenSayisiGenel * 100m / toplamRezervasyonSayisi, 2),
            CheckInOrani = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(checkInSayisi * 100m / toplamRezervasyonSayisi, 2),
            CheckOutOrani = toplamRezervasyonSayisi == 0 ? 0m : Math.Round(checkOutSayisi * 100m / toplamRezervasyonSayisi, 2),
            ToplamKisiSayisi = calisilmisRezervasyonlar.Sum(x => x.KisiSayisi),
            ToplamGeceSayisi = calisilmisRezervasyonlar.Sum(x => x.GeceSayisi)
        };

        return new RezervasyonDurumDagilimiRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Baslangic = baslangicGun,
            Bitis = bitisGun,
            OdaTipiId = odaTipiId,
            OdaTipiAdi = odaTipiAdi,
            Durum = durumFiltre,
            DurumLabel = durumConstant is not null ? RezervasyonDurumuLabel(durumConstant) : null,
            Baslik = $"{baslangicGun:dd.MM.yyyy} - {bitisGun:dd.MM.yyyy} REZERVASYON DURUM DAĞILIMI RAPORU",
            Ozet = ozet,
            Durumlar = durumlar,
            OdaTipleri = odaTipleri,
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

    private sealed class CalisilmisRezervasyon
    {
        public int RezervasyonId { get; set; }
        public string ReferansNo { get; set; } = string.Empty;
        public string MisafirAdiSoyadi { get; set; } = string.Empty;
        public DateTime GirisTarihi { get; set; }
        public DateTime CikisTarihi { get; set; }
        public int GeceSayisi { get; set; }
        public int KisiSayisi { get; set; }
        public string RezervasyonDurumu { get; set; } = string.Empty;
        public List<SegmentOdaKaydi> SegmentKayitlari { get; set; } = [];
    }
}
