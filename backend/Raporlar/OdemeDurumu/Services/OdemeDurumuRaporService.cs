using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.OdemeDurumu.Dto;
using STYS.Rezervasyonlar;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Raporlar.OdemeDurumu.Services;

public class OdemeDurumuRaporService : IOdemeDurumuRaporService
{
    private const int MaksimumGunAraligi = 366;

    private static readonly HashSet<string> GecerliOdemeDurumFiltreleri =
    [
        "tumu", "borclu", "odemesi-yok", "kismi-odendi", "tamamen-odendi", "cikis-yapmis-borclu"
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IDomainOperationLogger _domainLogger;

    public OdemeDurumuRaporService(
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

    public async Task<OdemeDurumuRaporDto> GetRaporAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? odemeDurumu,
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

        if ((bitis.Date - baslangic.Date).TotalDays > MaksimumGunAraligi)
        {
            throw new BaseException($"Tarih araligi en fazla {MaksimumGunAraligi} gun olabilir.", 400);
        }

        var filtre = string.IsNullOrWhiteSpace(odemeDurumu) ? "borclu" : odemeDurumu.Trim().ToLowerInvariant();
        if (!GecerliOdemeDurumFiltreleri.Contains(filtre))
        {
            throw new BaseException("Gecersiz odeme durumu filtresi.", 400);
        }

        var tesisAdi = await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var baslangicTarihi = baslangic.Date;
        var bitisTarihiExclusive = bitis.Date.AddDays(1);

        var rezervasyonlar = await _stysDbContext.Rezervasyonlar
            .AsNoTracking()
            .Where(r => r.TesisId == tesisId
                && r.AktifMi
                && r.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && r.GirisTarihi < bitisTarihiExclusive
                && r.CikisTarihi > baslangicTarihi)
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
                r.ParaBirimi
            })
            .ToListAsync(cancellationToken);

        var rezervasyonIds = rezervasyonlar.Select(x => x.Id).ToList();

        var odaAtamalari = await _stysDbContext.RezervasyonSegmentOdaAtamalari
            .AsNoTracking()
            .Where(a => rezervasyonIds.Contains(a.RezervasyonSegment!.RezervasyonId))
            .Select(a => new { a.RezervasyonSegment!.RezervasyonId, a.OdaNoSnapshot })
            .ToListAsync(cancellationToken);

        var odaNolariByRezervasyonId = odaAtamalari
            .GroupBy(x => x.RezervasyonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OdaNoSnapshot).Distinct().OrderBy(x => x).ToList());

        var odemeToplamlari = await _stysDbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(o => rezervasyonIds.Contains(o.RezervasyonId))
            .GroupBy(o => o.RezervasyonId)
            .Select(g => new
            {
                RezervasyonId = g.Key,
                Toplam = g.Sum(x => x.OdemeTutari),
                Sayi = g.Count(),
                SonTarih = g.Max(x => x.OdemeTarihi)
            })
            .ToDictionaryAsync(x => x.RezervasyonId, cancellationToken);

        var bugun = DateTime.Now.Date;

        // Borc durumu hesaplanirken rezervasyonun tum odeme kayitlari dikkate alinir; tarih araligi sadece rapora girecek rezervasyonlari belirler.
        var tumRezervasyonlar = rezervasyonlar.Select(r =>
        {
            var odemeBilgisi = odemeToplamlari.GetValueOrDefault(r.Id);
            var odenenTutar = odemeBilgisi?.Toplam ?? 0m;
            var odemeSayisi = odemeBilgisi?.Sayi ?? 0;
            var sonOdemeTarihi = odemeBilgisi?.SonTarih;
            var kalanTutar = Math.Max(0m, r.ToplamUcret - odenenTutar);

            string odemeDurumuKodu;
            string odemeDurumuLabel;
            if (odenenTutar <= 0m)
            {
                odemeDurumuKodu = "odemesi-yok";
                odemeDurumuLabel = "Ödemesi Yok";
            }
            else if (kalanTutar > 0m)
            {
                odemeDurumuKodu = "kismi-odendi";
                odemeDurumuLabel = "Kısmi Ödendi";
            }
            else
            {
                odemeDurumuKodu = "tamamen-odendi";
                odemeDurumuLabel = "Tamamen Ödendi";
            }

            var borcluMu = kalanTutar > 0m;
            var cikisYapmisMi = r.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi || r.CikisTarihi.Date < bugun;
            var cikisYapmisBorcluMu = cikisYapmisMi && kalanTutar > 0m;

            return new OdemeDurumuRezervasyonDto
            {
                RezervasyonId = r.Id,
                ReferansNo = r.ReferansNo,
                MisafirAdiSoyadi = r.MisafirAdiSoyadi,
                KurumUnite = null,
                GirisTarihi = r.GirisTarihi,
                CikisTarihi = r.CikisTarihi,
                RezervasyonDurumu = r.RezervasyonDurumu,
                RezervasyonDurumuLabel = RezervasyonDurumuLabel(r.RezervasyonDurumu),
                OdaNolari = odaNolariByRezervasyonId.GetValueOrDefault(r.Id, []),
                KisiSayisi = r.KisiSayisi,
                ToplamUcret = r.ToplamUcret,
                OdenenTutar = odenenTutar,
                KalanTutar = kalanTutar,
                ParaBirimi = string.IsNullOrWhiteSpace(r.ParaBirimi) ? "TRY" : r.ParaBirimi,
                OdemeDurumu = odemeDurumuKodu,
                OdemeDurumuLabel = odemeDurumuLabel,
                SonOdemeTarihi = sonOdemeTarihi,
                OdemeSayisi = odemeSayisi,
                BorcluMu = borcluMu,
                CikisYapmisMi = cikisYapmisMi,
                CikisYapmisBorcluMu = cikisYapmisBorcluMu
            };
        }).ToList();

        IEnumerable<OdemeDurumuRezervasyonDto> filtrelenmisSorgu = filtre switch
        {
            "tumu" => tumRezervasyonlar,
            "borclu" => tumRezervasyonlar.Where(x => x.BorcluMu),
            "odemesi-yok" => tumRezervasyonlar.Where(x => x.OdemeDurumu == "odemesi-yok"),
            "kismi-odendi" => tumRezervasyonlar.Where(x => x.OdemeDurumu == "kismi-odendi"),
            "tamamen-odendi" => tumRezervasyonlar.Where(x => x.OdemeDurumu == "tamamen-odendi"),
            "cikis-yapmis-borclu" => tumRezervasyonlar.Where(x => x.CikisYapmisBorcluMu),
            _ => tumRezervasyonlar
        };

        var siralanmisRezervasyonlar = filtrelenmisSorgu
            .OrderByDescending(x => x.CikisYapmisBorcluMu)
            .ThenByDescending(x => x.KalanTutar)
            .ThenBy(x => x.GirisTarihi)
            .ToList();

        var ozet = new OdemeDurumuOzetDto
        {
            ToplamRezervasyonSayisi = siralanmisRezervasyonlar.Count,
            BorcluRezervasyonSayisi = siralanmisRezervasyonlar.Count(x => x.BorcluMu),
            OdemesiOlmayanRezervasyonSayisi = siralanmisRezervasyonlar.Count(x => x.OdemeDurumu == "odemesi-yok"),
            KismiOdendiRezervasyonSayisi = siralanmisRezervasyonlar.Count(x => x.OdemeDurumu == "kismi-odendi"),
            TamamenOdendiRezervasyonSayisi = siralanmisRezervasyonlar.Count(x => x.OdemeDurumu == "tamamen-odendi"),
            CikisYapmisBorcluRezervasyonSayisi = siralanmisRezervasyonlar.Count(x => x.CikisYapmisBorcluMu),
            ToplamUcret = siralanmisRezervasyonlar.Sum(x => x.ToplamUcret),
            ToplamOdenenTutar = siralanmisRezervasyonlar.Sum(x => x.OdenenTutar),
            ToplamKalanTutar = siralanmisRezervasyonlar.Sum(x => x.KalanTutar),
            ParaBirimi = siralanmisRezervasyonlar.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };

        return new OdemeDurumuRaporDto
        {
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            Baslangic = baslangicTarihi,
            Bitis = bitis.Date,
            OdemeDurumu = filtre,
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
