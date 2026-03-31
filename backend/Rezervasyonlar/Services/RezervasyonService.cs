using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Data;
using System.Text.Json;
using STYS.AccessScope;
using STYS.Bildirimler;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.EkHizmetler.Entities;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using STYS.Fiyatlandirma;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri;
using STYS.KonaklamaTipleri.Entities;
using STYS.Odalar;
using STYS.OdaTipleri.Entities;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

public class RezervasyonService : IRezervasyonService
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IBildirimService _bildirimService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RezervasyonService(
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        IBildirimService bildirimService,
        IHttpContextAccessor httpContextAccessor)
    {
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
        _bildirimService = bildirimService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<RezervasyonTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler
            .Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonTesisDto
            {
                Id = x.Id,
                Ad = x.Ad,
                GirisSaati = x.GirisSaati,
                CikisSaati = x.CikisSaati
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonOdaTipiDto>> GetOdaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        return await _stysDbContext.OdaTipleri
            .Where(x => x.TesisId == tesisId && x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonOdaTipiDto
            {
                Id = x.Id,
                TesisId = x.TesisId,
                Ad = x.Ad,
                Kapasite = x.Kapasite,
                PaylasimliMi = x.PaylasimliMi
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonMisafirTipiDto>> GetMisafirTipleriAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        return await _stysDbContext.MisafirTipleri
            .Where(x => x.AktifMi
                && _stysDbContext.TesisMisafirTipleri.Any(y =>
                    y.TesisId == tesisId
                    && y.MisafirTipiId == x.Id
                    && y.AktifMi
                    && !y.IsDeleted))
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonMisafirTipiDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonKonaklamaTipiDto>> GetKonaklamaTipleriAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var rawItems = await _stysDbContext.KonaklamaTipleri
            .Where(x => x.AktifMi
                && _stysDbContext.TesisKonaklamaTipleri.Any(y =>
                    y.TesisId == tesisId
                    && y.KonaklamaTipiId == x.Id
                    && y.AktifMi
                    && !y.IsDeleted))
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Ad
            })
            .ToListAsync(cancellationToken);

        var effectiveIcerikMap = await GetEffectiveKonaklamaTipiIcerikMapAsync(
            tesisId,
            rawItems.Select(x => x.Id).ToList(),
            cancellationToken);

        return rawItems
            .Select(x => new RezervasyonKonaklamaTipiDto
            {
                Id = x.Id,
                Ad = x.Ad,
                IcerikKalemleri = effectiveIcerikMap.GetValueOrDefault(x.Id, [])
            })
            .ToList();
    }

    public async Task<List<RezervasyonIndirimKuraliSecenekDto>> GetUygulanabilirIndirimKurallariAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken = default)
    {
        if (misafirTipiId <= 0 || konaklamaTipiId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        await EnsureTesisHasMisafirTipiAsync(tesisId, misafirTipiId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(tesisId, konaklamaTipiId, cancellationToken);
        if (baslangicTarihi >= bitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        var rules = await QueryApplicableDiscountRulesAsync(
            tesisId,
            misafirTipiId,
            konaklamaTipiId,
            baslangicTarihi,
            bitisTarihi,
            cancellationToken);

        return rules
            .Select(x => new RezervasyonIndirimKuraliSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad,
                IndirimTipi = x.IndirimTipi,
                Deger = x.Deger,
                KapsamTipi = x.KapsamTipi,
                Oncelik = x.Oncelik,
                BirlesebilirMi = x.BirlesebilirMi
            })
            .ToList();
    }

    public async Task<List<RezervasyonListeDto>> GetRezervasyonlarAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            await EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
        }

        var query = _stysDbContext.Rezervasyonlar.AsQueryable();

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        var kayitlar = await query
            .OrderByDescending(x => x.GirisTarihi)
            .ThenByDescending(x => x.Id)
            .Select(x => new RezervasyonListeDto
            {
                Id = x.Id,
                ReferansNo = x.ReferansNo,
                TesisId = x.TesisId,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                MisafirTelefon = x.MisafirTelefon,
                MisafirEposta = x.MisafirEposta,
                TcKimlikNo = x.TcKimlikNo,
                PasaportNo = x.PasaportNo,
                MisafirCinsiyeti = x.MisafirCinsiyeti,
                KisiSayisi = x.KisiSayisi,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi,
                ToplamUcret = x.ToplamUcret,
                OdenenTutar = x.Odemeler
                    .Select(o => (decimal?)o.OdemeTutari)
                    .Sum() ?? 0m,
                KalanTutar = x.ToplamUcret - (x.Odemeler
                    .Select(o => (decimal?)o.OdemeTutari)
                    .Sum() ?? 0m),
                ParaBirimi = x.ParaBirimi,
                RezervasyonDurumu = x.RezervasyonDurumu,
                FiyatlamaOzeti = x.TekKisilikFiyatUygulandiMi
                    ? "Tek kisilik fiyat"
                    : x.Segmentler
                        .SelectMany(s => s.OdaAtamalari)
                        .Any(a => a.PaylasimliMiSnapshot)
                            ? x.Segmentler
                                .SelectMany(s => s.OdaAtamalari)
                                .Any(a => !a.PaylasimliMiSnapshot)
                                    ? "Karma"
                                    : "Kisi basi"
                            : "Ozel kullanim",
                KonaklayanPlaniTamamlandi = x.Segmentler.Count() > 0
                    && x.Konaklayanlar.Count() == x.KisiSayisi
                    && !x.Konaklayanlar.Any(k => k.AdSoyad == null || k.AdSoyad == string.Empty)
                    && !x.Konaklayanlar.Any(k =>
                        k.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                        && k.SegmentAtamalari.Count() != x.Segmentler.Count()),
                GelenKonaklayanSayisi = x.Konaklayanlar.Count(k => k.KatilimDurumu == KonaklayanKatilimDurumlari.Geldi),
                BekleyenKonaklayanSayisi = x.Konaklayanlar.Count(k => k.KatilimDurumu == null || k.KatilimDurumu == KonaklayanKatilimDurumlari.Bekleniyor),
                OdaDegisimiGerekli = false
            })
            .Take(200)
            .ToListAsync(cancellationToken);

        if (kayitlar.Count == 0)
        {
            return kayitlar;
        }

        var ekHizmetToplamlari = await GetEkHizmetToplamlariAsync(
            kayitlar.Select(x => x.Id).ToList(),
            cancellationToken);

        foreach (var kayit in kayitlar)
        {
            var ekHizmetToplami = ekHizmetToplamlari.TryGetValue(kayit.Id, out var toplam)
                ? toplam
                : 0m;

            kayit.ToplamUcret += ekHizmetToplami;
            kayit.KalanTutar = Math.Max(0m, kayit.ToplamUcret - kayit.OdenenTutar);
        }

        var affectedReservationIds = await GetReservationsRequiringRoomReassignmentAsync(
            kayitlar.Select(x => x.Id).ToList(),
            cancellationToken);

        foreach (var kayit in kayitlar)
        {
            kayit.OdaDegisimiGerekli = affectedReservationIds.Contains(kayit.Id);
        }

        return kayitlar;
    }

    public async Task<RezervasyonDashboardDto> GetGunlukDashboardAsync(
        int tesisId,
        DateTime? tarih,
        DateTime? kpiBaslangicTarihi,
        DateTime? kpiBitisTarihi,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis id.", 400);
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var gunBaslangic = (tarih ?? DateTime.Today).Date;
        var ertesiGunBaslangic = gunBaslangic.AddDays(1);
        var kpiBaslangic = (kpiBaslangicTarihi ?? new DateTime(gunBaslangic.Year, gunBaslangic.Month, 1)).Date;
        var kpiBitis = (kpiBitisTarihi ?? gunBaslangic).Date;
        var kpiBitisExclusive = kpiBitis.AddDays(1);

        if (kpiBaslangic >= kpiBitisExclusive)
        {
            throw new BaseException("KPI baslangic tarihi, bitis tarihinden buyuk olamaz.", 400);
        }

        if ((kpiBitisExclusive - kpiBaslangic).TotalDays > 366)
        {
            throw new BaseException("KPI tarih araligi en fazla 366 gun olabilir.", 400);
        }

        var checkInler = await _stysDbContext.Rezervasyonlar
            .Where(x =>
                x.AktifMi
                && x.TesisId == tesisId
                && x.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && x.GirisTarihi >= gunBaslangic
                && x.GirisTarihi < ertesiGunBaslangic)
            .OrderBy(x => x.GirisTarihi)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonDashboardKayitDto
            {
                Id = x.Id,
                ReferansNo = x.ReferansNo,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                KisiSayisi = x.KisiSayisi,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi,
                RezervasyonDurumu = x.RezervasyonDurumu
            })
            .ToListAsync(cancellationToken);

        var checkOutlar = await _stysDbContext.Rezervasyonlar
            .Where(x =>
                x.AktifMi
                && x.TesisId == tesisId
                && x.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && x.CikisTarihi >= gunBaslangic
                && x.CikisTarihi < ertesiGunBaslangic)
            .OrderBy(x => x.CikisTarihi)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonDashboardKayitDto
            {
                Id = x.Id,
                ReferansNo = x.ReferansNo,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                KisiSayisi = x.KisiSayisi,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi,
                RezervasyonDurumu = x.RezervasyonDurumu
            })
            .ToListAsync(cancellationToken);

        var toplamOdaSayisi = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && bina.TesisId == tesisId
            select oda.Id)
            .CountAsync(cancellationToken);

        var roomCapacities = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
                  && bina.TesisId == tesisId
            select new
            {
                OdaId = oda.Id,
                odaTipi.Kapasite
            })
            .ToListAsync(cancellationToken);

        var doluOdaSayisi = await (
            from atama in _stysDbContext.RezervasyonSegmentOdaAtamalari
            join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
            join rezervasyon in _stysDbContext.Rezervasyonlar on segment.RezervasyonId equals rezervasyon.Id
            where rezervasyon.AktifMi
                  && rezervasyon.TesisId == tesisId
                  && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                  && segment.BaslangicTarihi < ertesiGunBaslangic
                  && segment.BitisTarihi > gunBaslangic
            select atama.OdaId)
            .Distinct()
            .CountAsync(cancellationToken);

        var roomCapacityById = roomCapacities.ToDictionary(x => x.OdaId, x => x.Kapasite);
        var toplamKapasite = roomCapacities.Sum(x => x.Kapasite);
        var occupiedCapacityByRoom = await GetCurrentOccupancyByRoomAsync(
            roomCapacityById.Keys.ToList(),
            gunBaslangic,
            ertesiGunBaslangic,
            cancellationToken);
        var kullanilanKapasite = occupiedCapacityByRoom.Sum(x =>
        {
            var roomCapacity = roomCapacityById.GetValueOrDefault(x.Key);
            return roomCapacity <= 0 ? 0 : Math.Min(x.Value, roomCapacity);
        });
        var serbestKapasite = Math.Max(0, toplamKapasite - kullanilanKapasite);

        var kpiRezervasyonlar = await _stysDbContext.Rezervasyonlar
            .Where(x =>
                x.AktifMi
                && x.TesisId == tesisId
                && x.GirisTarihi < kpiBitisExclusive
                && x.CikisTarihi > kpiBaslangic)
            .Select(x => new
            {
                x.Id,
                x.RezervasyonDurumu,
                x.GirisTarihi,
                x.CikisTarihi
            })
            .ToListAsync(cancellationToken);

        var toplamRezervasyonSayisi = kpiRezervasyonlar.Count;
        var iptalRezervasyonSayisi = kpiRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.Iptal);
        var satilanGeceSayisi = kpiRezervasyonlar
            .Where(x => x.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
            .Sum(x => CalculateOverlapNights(x.GirisTarihi, x.CikisTarihi, kpiBaslangic, kpiBitisExclusive));
        var tarihAraligiGunSayisi = Math.Max(1, (kpiBitisExclusive - kpiBaslangic).Days);
        var toplamGeceSayisi = Math.Max(0, toplamOdaSayisi) * tarihAraligiGunSayisi;

        var kpiOdemeToplami = await _stysDbContext.RezervasyonOdemeler
            .Where(x =>
                x.Rezervasyon != null
                && x.Rezervasyon.AktifMi
                && x.Rezervasyon.TesisId == tesisId
                && x.OdemeTarihi >= kpiBaslangic
                && x.OdemeTarihi < kpiBitisExclusive)
            .SumAsync(x => (decimal?)x.OdemeTutari, cancellationToken) ?? 0m;

        var gunlukGelirMap = await _stysDbContext.RezervasyonOdemeler
            .Where(x =>
                x.Rezervasyon != null
                && x.Rezervasyon.AktifMi
                && x.Rezervasyon.TesisId == tesisId
                && x.OdemeTarihi >= kpiBaslangic
                && x.OdemeTarihi < kpiBitisExclusive)
            .GroupBy(x => x.OdemeTarihi.Date)
            .Select(group => new
            {
                Tarih = group.Key,
                Tutar = group.Sum(x => x.OdemeTutari)
            })
            .ToDictionaryAsync(x => x.Tarih, x => x.Tutar, cancellationToken);

        var odemeTipineGoreGelirKirilimi = await _stysDbContext.RezervasyonOdemeler
            .Where(x =>
                x.Rezervasyon != null
                && x.Rezervasyon.AktifMi
                && x.Rezervasyon.TesisId == tesisId
                && x.OdemeTarihi >= kpiBaslangic
                && x.OdemeTarihi < kpiBitisExclusive)
            .GroupBy(x => x.OdemeTipi)
            .Select(group => new RezervasyonGelirKirilimDto
            {
                Etiket = group.Key,
                Tutar = group.Sum(x => x.OdemeTutari)
            })
            .OrderByDescending(x => x.Tutar)
            .ThenBy(x => x.Etiket)
            .ToListAsync(cancellationToken);

        var durumaGoreRezervasyonKirilimi = kpiRezervasyonlar
            .GroupBy(x => x.RezervasyonDurumu)
            .Select(group => new RezervasyonGelirKirilimDto
            {
                Etiket = group.Key,
                Tutar = group.Count()
            })
            .OrderByDescending(x => x.Tutar)
            .ThenBy(x => x.Etiket)
            .ToList();

        var iptalOraniYuzde = toplamRezervasyonSayisi == 0
            ? 0m
            : Math.Round((decimal)iptalRezervasyonSayisi * 100m / toplamRezervasyonSayisi, 2, MidpointRounding.AwayFromZero);

        var dolulukOraniYuzde = toplamGeceSayisi == 0
            ? 0m
            : Math.Round((decimal)satilanGeceSayisi * 100m / toplamGeceSayisi, 2, MidpointRounding.AwayFromZero);

        var adr = satilanGeceSayisi == 0
            ? 0m
            : Math.Round(kpiOdemeToplami / satilanGeceSayisi, 2, MidpointRounding.AwayFromZero);

        var revPar = toplamGeceSayisi == 0
            ? 0m
            : Math.Round(kpiOdemeToplami / toplamGeceSayisi, 2, MidpointRounding.AwayFromZero);

        var kpiTrendGunluk = new List<RezervasyonKpiTrendGunDto>(tarihAraligiGunSayisi);
        for (var offset = 0; offset < tarihAraligiGunSayisi; offset++)
        {
            var gun = kpiBaslangic.AddDays(offset);
            var gunBitisExclusive = gun.AddDays(1);

            var gunlukRezervasyonlar = kpiRezervasyonlar
                .Where(x => x.GirisTarihi < gunBitisExclusive && x.CikisTarihi > gun)
                .ToList();

            var gunlukRezervasyonSayisi = gunlukRezervasyonlar.Count(x => x.RezervasyonDurumu != RezervasyonDurumlari.Iptal);
            var gunlukIptalSayisi = gunlukRezervasyonlar.Count(x => x.RezervasyonDurumu == RezervasyonDurumlari.Iptal);
            var gunlukSatilanGece = gunlukRezervasyonlar
                .Where(x => x.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
                .Sum(x => CalculateOverlapNights(x.GirisTarihi, x.CikisTarihi, gun, gunBitisExclusive));

            var gunlukDolulukOrani = toplamOdaSayisi == 0
                ? 0m
                : Math.Round((decimal)gunlukSatilanGece * 100m / toplamOdaSayisi, 2, MidpointRounding.AwayFromZero);

            var gunlukGelir = gunlukGelirMap.TryGetValue(gun, out var gelir) ? gelir : 0m;

            kpiTrendGunluk.Add(new RezervasyonKpiTrendGunDto
            {
                Tarih = gun,
                Gelir = gunlukGelir,
                RezervasyonSayisi = gunlukRezervasyonSayisi,
                IptalSayisi = gunlukIptalSayisi,
                SatilanGeceSayisi = gunlukSatilanGece,
                DolulukOraniYuzde = gunlukDolulukOrani
            });
        }

        return new RezervasyonDashboardDto
        {
            TesisId = tesisId,
            Tarih = gunBaslangic,
            KpiBaslangicTarihi = kpiBaslangic,
            KpiBitisTarihi = kpiBitis,
            ToplamOdaSayisi = toplamOdaSayisi,
            DoluOdaSayisi = doluOdaSayisi,
            BosOdaSayisi = Math.Max(0, toplamOdaSayisi - doluOdaSayisi),
            ToplamKapasite = toplamKapasite,
            KullanilanKapasite = kullanilanKapasite,
            SerbestKapasite = serbestKapasite,
            KpiOzet = new RezervasyonKpiOzetDto
            {
                TarihAraligiGunSayisi = tarihAraligiGunSayisi,
                ToplamRezervasyonSayisi = toplamRezervasyonSayisi,
                IptalRezervasyonSayisi = iptalRezervasyonSayisi,
                IptalOraniYuzde = iptalOraniYuzde,
                ToplamGeceSayisi = toplamGeceSayisi,
                SatilanGeceSayisi = satilanGeceSayisi,
                DolulukOraniYuzde = dolulukOraniYuzde,
                ToplamGelir = kpiOdemeToplami,
                Adr = adr,
                RevPar = revPar
            },
            OdemeTipineGoreGelirKirilimi = odemeTipineGoreGelirKirilimi,
            DurumaGoreRezervasyonKirilimi = durumaGoreRezervasyonKirilimi,
            KpiTrendGunluk = kpiTrendGunluk,
            BugunCheckInler = checkInler,
            BugunCheckOutlar = checkOutlar
        };
    }

    public async Task<RezervasyonDetayDto?> GetRezervasyonDetayAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        var raw = await query
            .Select(x => new
            {
                x.Id,
                x.ReferansNo,
                x.TesisId,
                x.RezervasyonDurumu,
                x.MisafirAdiSoyadi,
                x.MisafirCinsiyeti,
                x.KisiSayisi,
                x.KonaklamaTipiId,
                KonaklamaTipiAdi = x.KonaklamaTipi != null ? x.KonaklamaTipi.Ad : null,
                x.TekKisilikFiyatUygulandiMi,
                x.GirisTarihi,
                x.CikisTarihi,
                x.ToplamBazUcret,
                x.ToplamUcret,
                x.ParaBirimi,
                x.UygulananIndirimlerJson,
                KonaklamaHaklari = x.KonaklamaHaklari
                    .Where(h => !h.IsDeleted && h.AktifMi)
                    .OrderBy(h => h.HakTarihi)
                    .ThenBy(h => h.HizmetAdiSnapshot)
                    .ThenBy(h => h.Id)
                    .Select(h => new
                    {
                        Id = h.Id,
                        HizmetKodu = h.HizmetKodu,
                        HizmetAdi = h.HizmetAdiSnapshot,
                        Miktar = h.Miktar,
                        Periyot = h.Periyot,
                        PeriyotAdi = h.PeriyotAdiSnapshot,
                        KullanimTipi = h.KullanimTipi,
                        KullanimTipiAdi = h.KullanimTipiAdiSnapshot,
                        KullanimNoktasi = h.KullanimNoktasi,
                        KullanimNoktasiAdi = h.KullanimNoktasiAdiSnapshot,
                        h.KullanimBaslangicSaati,
                        h.KullanimBitisSaati,
                        CheckInGunuGecerliMi = h.CheckInGunuGecerliMi,
                        CheckOutGunuGecerliMi = h.CheckOutGunuGecerliMi,
                        HakTarihi = h.HakTarihi,
                        Aciklama = h.AciklamaSnapshot,
                        Durum = h.Durum,
                        TuketimKayitlari = h.TuketimKayitlari
                            .Where(t => !t.IsDeleted && t.AktifMi)
                            .OrderByDescending(t => t.TuketimTarihi)
                            .ThenByDescending(t => t.Id)
                            .Select(t => new
                            {
                                t.Id,
                                t.IsletmeAlaniId,
                                t.TuketimTarihi,
                                t.Miktar,
                                t.KullanimTipi,
                                t.KullanimNoktasi,
                                KullanimNoktasiAdi = t.KullanimNoktasiAdiSnapshot,
                                t.TuketimNoktasiAdi,
                                t.Aciklama,
                                t.CreatedBy,
                                t.CreatedAt
                            })
                            .ToList()
                    })
                    .ToList(),
                Segmentler = x.Segmentler
                    .OrderBy(s => s.SegmentSirasi)
                    .ThenBy(s => s.Id)
                    .Select(s => new RezervasyonDetaySegmentDto
                    {
                        SegmentSirasi = s.SegmentSirasi,
                        BaslangicTarihi = s.BaslangicTarihi,
                        BitisTarihi = s.BitisTarihi,
                        OdaAtamalari = s.OdaAtamalari
                            .OrderBy(a => a.OdaId)
                            .ThenBy(a => a.Id)
                            .Select(a => new RezervasyonDetayOdaAtamaDto
                            {
                                OdaId = a.OdaId,
                                OdaNo = a.OdaNoSnapshot,
                                BinaAdi = a.BinaAdiSnapshot,
                                OdaTipiAdi = a.OdaTipiAdiSnapshot,
                                AyrilanKisiSayisi = a.AyrilanKisiSayisi,
                                Kapasite = a.KapasiteSnapshot,
                                PaylasimliMi = a.PaylasimliMiSnapshot
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        var ekHizmetler = await GetEkHizmetlerAsync(rezervasyonId, cancellationToken);
        var ekHizmetToplami = ekHizmetler.Sum(x => x.ToplamTutar);
        var tuketimNoktalari = await GetTuketimNoktalariAsync(raw.TesisId, cancellationToken);
        var konaklamaTipiIcerikKalemleri = raw.KonaklamaTipiId.HasValue
            ? (await GetEffectiveKonaklamaTipiIcerikMapAsync(raw.TesisId, [raw.KonaklamaTipiId.Value], cancellationToken))
                .GetValueOrDefault(raw.KonaklamaTipiId.Value, [])
            : [];

        return new RezervasyonDetayDto
        {
            Id = raw.Id,
            ReferansNo = raw.ReferansNo,
            TesisId = raw.TesisId,
            RezervasyonDurumu = raw.RezervasyonDurumu,
            MisafirAdiSoyadi = raw.MisafirAdiSoyadi,
            MisafirCinsiyeti = raw.MisafirCinsiyeti,
            KisiSayisi = raw.KisiSayisi,
            KonaklamaTipiAdi = raw.KonaklamaTipiAdi,
            TekKisilikFiyatUygulandiMi = raw.TekKisilikFiyatUygulandiMi,
            FiyatlamaOzeti = BuildRezervasyonFiyatlamaOzeti(raw.TekKisilikFiyatUygulandiMi, raw.Segmentler),
            KonaklamaTipiIcerikKalemleri = konaklamaTipiIcerikKalemleri,
            GirisTarihi = raw.GirisTarihi,
            CikisTarihi = raw.CikisTarihi,
            KonaklamaUcreti = raw.ToplamUcret,
            EkHizmetToplami = ekHizmetToplami,
            ToplamBazUcret = raw.ToplamBazUcret,
            ToplamUcret = raw.ToplamUcret + ekHizmetToplami,
            ParaBirimi = raw.ParaBirimi,
            KonaklamaHaklari = raw.KonaklamaHaklari
                .Select(x =>
                {
                    var tuketilenMiktar = x.TuketimKayitlari.Sum(t => t.Miktar);

                    return new RezervasyonKonaklamaHakkiDto
                    {
                        Id = x.Id,
                        HizmetKodu = x.HizmetKodu,
                        HizmetAdi = x.HizmetAdi,
                        Miktar = x.Miktar,
                        Periyot = x.Periyot,
                        PeriyotAdi = x.PeriyotAdi,
                        KullanimTipi = x.KullanimTipi,
                        KullanimTipiAdi = x.KullanimTipiAdi,
                        KullanimNoktasi = x.KullanimNoktasi,
                        KullanimNoktasiAdi = x.KullanimNoktasiAdi,
                        KullanimBaslangicSaati = FormatTime(x.KullanimBaslangicSaati),
                        KullanimBitisSaati = FormatTime(x.KullanimBitisSaati),
                        CheckInGunuGecerliMi = x.CheckInGunuGecerliMi,
                        CheckOutGunuGecerliMi = x.CheckOutGunuGecerliMi,
                        HakTarihi = x.HakTarihi,
                        Aciklama = x.Aciklama,
                        Durum = x.Durum,
                        TuketilenMiktar = tuketilenMiktar,
                        KalanMiktar = x.KullanimTipi == KonaklamaTipiIcerikKullanimTipleri.Adetli
                            ? Math.Max(0, x.Miktar - tuketilenMiktar)
                            : null,
                        TuketimNoktalari = GetApplicableTuketimNoktalari(tuketimNoktalari, x.KullanimNoktasi)
                            .ToList(),
                        SonTuketimTarihi = x.TuketimKayitlari
                            .Select(t => (DateTime?)t.TuketimTarihi)
                            .FirstOrDefault(),
                        TuketimKayitlari = x.TuketimKayitlari
                            .Select(t => new RezervasyonKonaklamaHakkiTuketimKaydiDto
                            {
                                Id = t.Id,
                                IsletmeAlaniId = t.IsletmeAlaniId,
                                TuketimTarihi = t.TuketimTarihi,
                                Miktar = t.Miktar,
                                KullanimTipi = t.KullanimTipi,
                                KullanimNoktasi = t.KullanimNoktasi,
                                KullanimNoktasiAdi = t.KullanimNoktasiAdi,
                                TuketimNoktasiAdi = t.TuketimNoktasiAdi,
                                Aciklama = t.Aciklama,
                                CreatedBy = t.CreatedBy ?? string.Empty,
                                CreatedAt = t.CreatedAt
                            })
                            .ToList()
                    };
                })
                .ToList(),
            UygulananIndirimler = DeserializeAppliedDiscounts(raw.UygulananIndirimlerJson),
            EkHizmetler = ekHizmetler,
            Segmentler = raw.Segmentler
        };
    }

    public async Task<List<RezervasyonDegisiklikGecmisiDto>> GetDegisiklikGecmisiAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        return await _stysDbContext.RezervasyonDegisiklikGecmisleri
            .Where(x => x.RezervasyonId == rezervasyonId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new RezervasyonDegisiklikGecmisiDto
            {
                Id = x.Id,
                IslemTipi = x.IslemTipi,
                Aciklama = x.Aciklama,
                OncekiDegerJson = x.OncekiDegerJson,
                YeniDegerJson = x.YeniDegerJson,
                CreatedAt = x.CreatedAt ?? DateTime.MinValue,
                CreatedBy = x.CreatedBy ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RezervasyonKonaklayanPlanDto?> GetKonaklayanPlaniAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        var raw = await query
            .Select(x => new
            {
                x.Id,
                x.KisiSayisi,
                x.MisafirAdiSoyadi,
                x.TcKimlikNo,
                x.PasaportNo,
                x.MisafirCinsiyeti,
                Segmentler = x.Segmentler
                    .OrderBy(s => s.SegmentSirasi)
                    .ThenBy(s => s.Id)
                    .Select(s => new
                    {
                        SegmentId = s.Id,
                        s.SegmentSirasi,
                        s.BaslangicTarihi,
                        s.BitisTarihi,
                        OdaSecenekleri = s.OdaAtamalari
                            .OrderBy(a => a.OdaNoSnapshot)
                            .ThenBy(a => a.Id)
                            .Select(a => new
                            {
                                a.OdaId,
                                a.OdaNoSnapshot,
                                a.BinaAdiSnapshot,
                                a.OdaTipiAdiSnapshot,
                                a.AyrilanKisiSayisi,
                                a.PaylasimliMiSnapshot
                            })
                            .ToList()
                    })
                    .ToList(),
                Konaklayanlar = x.Konaklayanlar
                    .OrderBy(k => k.SiraNo)
                    .ThenBy(k => k.Id)
                    .Select(k => new
                    {
                        k.SiraNo,
                        k.AdSoyad,
                        k.TcKimlikNo,
                        k.PasaportNo,
                        k.Cinsiyet,
                        k.KatilimDurumu,
                        Atamalar = k.SegmentAtamalari
                            .Select(a => new
                            {
                                a.RezervasyonSegmentId,
                                a.OdaId,
                                a.YatakNo
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        var segments = raw.Segmentler
            .Select(s => new RezervasyonKonaklayanSegmentDto
            {
                SegmentId = s.SegmentId,
                SegmentSirasi = s.SegmentSirasi,
                BaslangicTarihi = s.BaslangicTarihi,
                BitisTarihi = s.BitisTarihi,
                OdaSecenekleri = s.OdaSecenekleri
                    .Select(o => new RezervasyonKonaklayanOdaSecenekDto
                    {
                        OdaId = o.OdaId,
                        OdaNo = o.OdaNoSnapshot,
                        BinaAdi = o.BinaAdiSnapshot,
                        OdaTipiAdi = o.OdaTipiAdiSnapshot,
                        AyrilanKisiSayisi = o.AyrilanKisiSayisi,
                        PaylasimliMi = o.PaylasimliMiSnapshot
                    })
                    .ToList()
            })
            .ToList();

        var segmentIds = segments
            .Select(x => x.SegmentId)
            .ToHashSet();

        var konaklayanlar = raw.Konaklayanlar
            .Select(k =>
            {
                var atamaBySegment = k.Atamalar
                    .Where(a => segmentIds.Contains(a.RezervasyonSegmentId))
                    .GroupBy(a => a.RezervasyonSegmentId)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var first = g.First();
                        return (OdaId: (int?)first.OdaId, first.YatakNo);
                    });

                var normalizedAtamalar = segments
                    .Select(s => new RezervasyonKonaklayanKisiAtamaDto
                    {
                        SegmentId = s.SegmentId,
                        OdaId = atamaBySegment.TryGetValue(s.SegmentId, out var atama) ? atama.OdaId : null,
                        YatakNo = atamaBySegment.TryGetValue(s.SegmentId, out var yatakAtama) ? yatakAtama.YatakNo : null
                    })
                    .ToList();

                return new RezervasyonKonaklayanKisiDto
                {
                    SiraNo = k.SiraNo,
                    AdSoyad = k.AdSoyad,
                    TcKimlikNo = k.TcKimlikNo,
                    PasaportNo = k.PasaportNo,
                    Cinsiyet = k.Cinsiyet,
                    KatilimDurumu = NormalizeStoredKonaklayanKatilimDurumu(k.KatilimDurumu),
                    Atamalar = normalizedAtamalar
                };
            })
            .ToList();

        if (konaklayanlar.Count == 0)
        {
            for (var index = 1; index <= raw.KisiSayisi; index++)
            {
                konaklayanlar.Add(new RezervasyonKonaklayanKisiDto
                {
                    SiraNo = index,
                    AdSoyad = index == 1 ? raw.MisafirAdiSoyadi : string.Empty,
                    TcKimlikNo = index == 1 ? raw.TcKimlikNo : null,
                    PasaportNo = index == 1 ? raw.PasaportNo : null,
                    Cinsiyet = index == 1 ? raw.MisafirCinsiyeti : null,
                    KatilimDurumu = KonaklayanKatilimDurumlari.Bekleniyor,
                    Atamalar = segments
                        .Select(s => new RezervasyonKonaklayanKisiAtamaDto
                        {
                            SegmentId = s.SegmentId,
                            OdaId = null,
                            YatakNo = null
                        })
                        .ToList()
                });
            }
        }
        else
        {
            for (var index = 1; index <= raw.KisiSayisi; index++)
            {
                if (konaklayanlar.Any(x => x.SiraNo == index))
                {
                    continue;
                }

                konaklayanlar.Add(new RezervasyonKonaklayanKisiDto
                {
                    SiraNo = index,
                    AdSoyad = string.Empty,
                    Cinsiyet = index == 1 ? raw.MisafirCinsiyeti : null,
                    KatilimDurumu = KonaklayanKatilimDurumlari.Bekleniyor,
                    Atamalar = segments
                        .Select(s => new RezervasyonKonaklayanKisiAtamaDto
                        {
                            SegmentId = s.SegmentId,
                            OdaId = null,
                            YatakNo = null
                        })
                        .ToList()
                });
            }

            konaklayanlar = konaklayanlar
                .OrderBy(x => x.SiraNo)
                .Take(raw.KisiSayisi)
                .ToList();
        }

        return new RezervasyonKonaklayanPlanDto
        {
            RezervasyonId = raw.Id,
            KisiSayisi = raw.KisiSayisi,
            Segmentler = segments,
            Konaklayanlar = konaklayanlar
        };
    }

    public async Task<RezervasyonKonaklayanPlanDto> KaydetKonaklayanPlaniAsync(int rezervasyonId, RezervasyonKonaklayanPlanKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        if (request.Konaklayanlar.Count == 0)
        {
            throw new BaseException("En az bir konaklayan kaydi zorunludur.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        var reservationRaw = await query
            .Select(x => new
            {
                x.Id,
                x.KisiSayisi,
                x.RezervasyonDurumu,
                Segmentler = x.Segmentler
                    .Select(s => new
                    {
                        SegmentId = s.Id,
                        s.BaslangicTarihi,
                        s.BitisTarihi,
                        OdaKapasiteleri = s.OdaAtamalari
                            .Select(a => new { a.OdaId, a.AyrilanKisiSayisi, a.PaylasimliMiSnapshot })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (reservationRaw is null)
        {
            throw new BaseException("Rezervasyon bulunamadi.", 404);
        }

        if (request.Konaklayanlar.Count != reservationRaw.KisiSayisi)
        {
            throw new BaseException("Konaklayan kayit sayisi rezervasyon kisi sayisina esit olmalidir.", 400);
        }

        var segmentIds = reservationRaw.Segmentler
            .Select(x => x.SegmentId)
            .ToHashSet();

        if (segmentIds.Count == 0)
        {
            throw new BaseException("Rezervasyon segment bilgisi bulunamadi.", 400);
        }

        var sortedGuests = request.Konaklayanlar
            .OrderBy(x => x.SiraNo)
            .ToList();

        var distinctSiraCount = sortedGuests
            .Select(x => x.SiraNo)
            .Distinct()
            .Count();

        if (distinctSiraCount != sortedGuests.Count)
        {
            throw new BaseException("Konaklayan sira numaralari benzersiz olmali.", 400);
        }

        if (sortedGuests.Any(x => x.SiraNo <= 0 || x.SiraNo > reservationRaw.KisiSayisi))
        {
            throw new BaseException("Konaklayan sira numaralari gecersiz.", 400);
        }

        if (sortedGuests.Any(x => string.IsNullOrWhiteSpace(x.AdSoyad)))
        {
            throw new BaseException("Tum konaklayanlar icin ad soyad bilgisi zorunludur.", 400);
        }

        var normalizedGenderBySiraNo = sortedGuests.ToDictionary(
            x => x.SiraNo,
            x => NormalizeKonaklayanCinsiyet(x.Cinsiyet));
        var normalizedParticipationBySiraNo = sortedGuests.ToDictionary(
            x => x.SiraNo,
            x => NormalizeKonaklayanKatilimDurumu(x.KatilimDurumu));

        if (reservationRaw.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi
            && normalizedParticipationBySiraNo.Values.Any(x => x == KonaklayanKatilimDurumlari.Ayrildi))
        {
            throw new BaseException("Konaklayan katilim durumu Ayrildi yalnizca check-out sonrasinda kullanilabilir.", 400);
        }

        var odaBilgiBySegment = reservationRaw.Segmentler
            .ToDictionary(
                x => x.SegmentId,
                x => x.OdaKapasiteleri
                    .GroupBy(y => y.OdaId)
                    .ToDictionary(
                        g => g.Key,
                        g => (
                            AssignedCapacity: g.Sum(z => z.AyrilanKisiSayisi),
                            PaylasimliMi: g.Any(z => z.PaylasimliMiSnapshot))));
        var segmentDateById = reservationRaw.Segmentler.ToDictionary(
            x => x.SegmentId,
            x => (x.BaslangicTarihi, x.BitisTarihi));

        var sharedRoomAssignments = sortedGuests
            .SelectMany(guest => guest.Atamalar
                .Where(atama =>
                    atama.OdaId.HasValue
                    && atama.OdaId.Value > 0
                    && odaBilgiBySegment.TryGetValue(atama.SegmentId, out var segmentRooms)
                    && segmentRooms.TryGetValue(atama.OdaId.Value, out var roomInfo)
                    && roomInfo.PaylasimliMi)
                .Select(atama => new SharedRoomGuestSelection(
                    guest.SiraNo,
                    normalizedGenderBySiraNo[guest.SiraNo],
                    atama.SegmentId,
                    atama.OdaId!.Value)))
            .ToList();

        var existingSharedRoomOccupancies = await GetSharedRoomGuestOccupanciesAsync(
            sharedRoomAssignments.Select(x => x.OdaId).Distinct().ToList(),
            segmentDateById.Values.Min(x => x.BaslangicTarihi),
            segmentDateById.Values.Max(x => x.BitisTarihi),
            cancellationToken,
            rezervasyonId);
        var currentPlanRoomGenderBySegment = new Dictionary<(int SegmentId, int OdaId), string>();

        var occupancyCounter = new Dictionary<(int SegmentId, int OdaId), int>();
        var bedOccupancyCounter = new HashSet<(int SegmentId, int OdaId, int YatakNo)>();

        foreach (var guest in sortedGuests)
        {
            var katilimDurumu = normalizedParticipationBySiraNo[guest.SiraNo];
            var assignmentRequired = DoesGuestRequireAssignments(katilimDurumu);
            if (guest.Atamalar.Count != segmentIds.Count)
            {
                throw new BaseException("Her konaklayan icin tum segmentlere oda atamasi yapilmalidir.", 400);
            }

            var guestSegmentIds = guest.Atamalar
                .Select(x => x.SegmentId)
                .ToList();

            if (guestSegmentIds.Distinct().Count() != guestSegmentIds.Count || guestSegmentIds.Any(x => !segmentIds.Contains(x)))
            {
                throw new BaseException("Konaklayan segment atamalari gecersiz.", 400);
            }

            foreach (var atama in guest.Atamalar)
            {
                if (!assignmentRequired)
                {
                    continue;
                }

                if (!atama.OdaId.HasValue || atama.OdaId.Value <= 0)
                {
                    throw new BaseException("Her segment icin oda secimi zorunludur.", 400);
                }

                var segmentOdaBilgisi = odaBilgiBySegment[atama.SegmentId];
                if (!segmentOdaBilgisi.TryGetValue(atama.OdaId.Value, out var odaBilgisi))
                {
                    throw new BaseException("Secilen oda ilgili segmentte mevcut degil.", 400);
                }

                var key = (atama.SegmentId, atama.OdaId.Value);
                var current = occupancyCounter.TryGetValue(key, out var count) ? count : 0;
                occupancyCounter[key] = current + 1;

                if (occupancyCounter[key] > odaBilgisi.AssignedCapacity)
                {
                    throw new BaseException("Secilen oda icin kisi kapasitesi asildi.", 400);
                }

                if (odaBilgisi.PaylasimliMi)
                {
                    var guestGender = normalizedGenderBySiraNo[guest.SiraNo];
                    if (guestGender is null)
                    {
                        throw new BaseException("Paylasimli oda icin konaklayan cinsiyeti zorunludur.", 400);
                    }

                    var segmentDates = segmentDateById[atama.SegmentId];
                    var existingRoomGenders = GetDistinctSharedRoomGenders(
                        existingSharedRoomOccupancies,
                        atama.OdaId.Value,
                        segmentDates.BaslangicTarihi,
                        segmentDates.BitisTarihi);
                    if (existingRoomGenders.Count > 1)
                    {
                        throw new BaseException("Secilen paylasimli odada cinsiyet dagilimi tutarsiz. Lutfen farkli bir oda seciniz.", 400);
                    }

                    var roomGenderKey = (atama.SegmentId, atama.OdaId.Value);
                    var roomGender = existingRoomGenders.Count == 1
                        ? existingRoomGenders.Single()
                        : currentPlanRoomGenderBySegment.GetValueOrDefault(roomGenderKey);
                    if (roomGender is not null && !string.Equals(roomGender, guestGender, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new BaseException("Paylasimli odada farkli cinsiyetten konaklayanlar ayni odada kalamaz.", 400);
                    }

                    currentPlanRoomGenderBySegment[roomGenderKey] = roomGender ?? guestGender;

                    if (!atama.YatakNo.HasValue || atama.YatakNo.Value <= 0)
                    {
                        throw new BaseException("Paylasimli oda icin yatak secimi zorunludur.", 400);
                    }

                    if (atama.YatakNo.Value > odaBilgisi.AssignedCapacity)
                    {
                        throw new BaseException("Secilen yatak numarasi gecersiz.", 400);
                    }

                    var bedKey = (atama.SegmentId, atama.OdaId.Value, atama.YatakNo.Value);
                    if (!bedOccupancyCounter.Add(bedKey))
                    {
                        throw new BaseException("Ayni segmentte ayni yatak birden fazla kisiye atanamaz.", 400);
                    }
                }
            }
        }

        var existingAssignments = await (
            from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
            join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
            where konaklayan.RezervasyonId == rezervasyonId
            select atama)
            .ToListAsync(cancellationToken);

        if (existingAssignments.Count > 0)
        {
            _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.RemoveRange(existingAssignments);
        }

        var existingGuests = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId)
            .ToListAsync(cancellationToken);

        var oldPlanSnapshot = existingGuests
            .OrderBy(x => x.SiraNo)
            .Select(x => new
            {
                x.SiraNo,
                x.AdSoyad,
                x.TcKimlikNo,
                x.PasaportNo,
                x.Cinsiyet,
                x.KatilimDurumu,
                Atamalar = existingAssignments
                    .Where(a => a.RezervasyonKonaklayanId == x.Id)
                    .OrderBy(a => a.RezervasyonSegmentId)
                    .ThenBy(a => a.OdaId)
                    .Select(a => new
                    {
                        a.RezervasyonSegmentId,
                        a.OdaId,
                        a.YatakNo
                    })
                    .ToList()
            })
            .ToList();

        if (existingGuests.Count > 0)
        {
            _stysDbContext.RezervasyonKonaklayanlar.RemoveRange(existingGuests);
        }

        var guestsBySira = sortedGuests.ToDictionary(
            x => x.SiraNo,
            x => new RezervasyonKonaklayan
            {
                RezervasyonId = rezervasyonId,
                SiraNo = x.SiraNo,
                AdSoyad = x.AdSoyad.Trim(),
                TcKimlikNo = string.IsNullOrWhiteSpace(x.TcKimlikNo) ? null : x.TcKimlikNo.Trim(),
                PasaportNo = string.IsNullOrWhiteSpace(x.PasaportNo) ? null : x.PasaportNo.Trim(),
                Cinsiyet = normalizedGenderBySiraNo[x.SiraNo],
                KatilimDurumu = normalizedParticipationBySiraNo[x.SiraNo]
            });

        await _stysDbContext.RezervasyonKonaklayanlar.AddRangeAsync(guestsBySira.Values, cancellationToken);

        foreach (var guest in sortedGuests)
        {
            var guestEntity = guestsBySira[guest.SiraNo];
            if (!DoesGuestRequireAssignments(normalizedParticipationBySiraNo[guest.SiraNo]))
            {
                continue;
            }

            foreach (var atama in guest.Atamalar)
            {
                var odaBilgisi = odaBilgiBySegment[atama.SegmentId][atama.OdaId!.Value];
                _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
                {
                    RezervasyonKonaklayan = guestEntity,
                    RezervasyonSegmentId = atama.SegmentId,
                    OdaId = atama.OdaId!.Value,
                    YatakNo = odaBilgisi.PaylasimliMi ? atama.YatakNo : null
                });
            }
        }

        var newPlanSnapshot = sortedGuests
            .Select(x => new
            {
                x.SiraNo,
                AdSoyad = x.AdSoyad.Trim(),
                TcKimlikNo = string.IsNullOrWhiteSpace(x.TcKimlikNo) ? null : x.TcKimlikNo.Trim(),
                PasaportNo = string.IsNullOrWhiteSpace(x.PasaportNo) ? null : x.PasaportNo.Trim(),
                Cinsiyet = normalizedGenderBySiraNo[x.SiraNo],
                KatilimDurumu = normalizedParticipationBySiraNo[x.SiraNo],
                Atamalar = DoesGuestRequireAssignments(normalizedParticipationBySiraNo[x.SiraNo])
                    ? x.Atamalar
                    .OrderBy(a => a.SegmentId)
                    .ThenBy(a => a.OdaId)
                    .Select(a => new
                    {
                        RezervasyonSegmentId = a.SegmentId,
                        OdaId = a.OdaId,
                        YatakNo = a.OdaId.HasValue && a.OdaId.Value > 0
                            ? (odaBilgiBySegment[a.SegmentId][a.OdaId.Value].PaylasimliMi ? a.YatakNo : null)
                            : null
                    })
                    .ToList()
                    : x.Atamalar
                        .OrderBy(a => a.SegmentId)
                        .Select(a => new
                        {
                            RezervasyonSegmentId = a.SegmentId,
                            OdaId = (int?)null,
                            YatakNo = (int?)null
                        })
                        .ToList()
            })
            .ToList();

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.KonaklayanPlaniKaydedildi,
            "Konaklayan plani kaydedildi.",
            oldPlanSnapshot,
            newPlanSnapshot);

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetKonaklayanPlaniAsync(rezervasyonId, cancellationToken)
               ?? throw new BaseException("Konaklayan plani kaydedildi ancak tekrar okunamadi.", 500);
    }

    public async Task<RezervasyonOdaDegisimSecenekDto> GetOdaDegisimSecenekleriAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        EnsureCanChangeRoomForReservationStatus(reservation.RezervasyonDurumu);

        var assignments = await GetReservationSegmentAssignmentsAsync(rezervasyonId, cancellationToken);
        if (assignments.Count == 0)
        {
            throw new BaseException("Rezervasyon segment/oda atama kaydi bulunamadi.", 400);
        }

        var problematicAssignmentIds = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                join blok in _stysDbContext.OdaKullanimBloklari on atama.OdaId equals blok.OdaId
                where segment.RezervasyonId == rezervasyonId
                      && blok.AktifMi
                      && blok.BaslangicTarihi < segment.BitisTarihi
                      && blok.BitisTarihi > segment.BaslangicTarihi
                select atama.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (problematicAssignmentIds.Count == 0)
        {
            return new RezervasyonOdaDegisimSecenekDto
            {
                RezervasyonId = reservation.Id,
                ReferansNo = reservation.ReferansNo
            };
        }

        var problematicSet = problematicAssignmentIds.ToHashSet();
        var groupedBySegment = assignments
            .GroupBy(x => x.SegmentId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var guestAssignments = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                where konaklayan.RezervasyonId == rezervasyonId
                select new
                {
                    atama.RezervasyonSegmentId,
                    atama.OdaId,
                    konaklayan.SiraNo,
                    konaklayan.AdSoyad,
                    konaklayan.Cinsiyet,
                    atama.YatakNo
                })
            .ToListAsync(cancellationToken);

        var kayitlar = new List<RezervasyonOdaDegisimKayitDto>();
        foreach (var assignment in assignments.Where(x => problematicSet.Contains(x.RezervasyonSegmentOdaAtamaId)))
        {
            var segmentAssignments = groupedBySegment[assignment.SegmentId];
            var adayOdalar = await GetReplacementCandidatesForAssignmentAsync(
                reservation.Id,
                reservation.TesisId,
                assignment,
                segmentAssignments,
                guestAssignments
                    .Where(x => x.RezervasyonSegmentId == assignment.SegmentId && x.OdaId == assignment.OdaId)
                    .Select(x => x.Cinsiyet)
                    .ToList(),
                cancellationToken);

            kayitlar.Add(new RezervasyonOdaDegisimKayitDto
            {
                RezervasyonSegmentOdaAtamaId = assignment.RezervasyonSegmentOdaAtamaId,
                SegmentId = assignment.SegmentId,
                SegmentSirasi = assignment.SegmentSirasi,
                BaslangicTarihi = assignment.BaslangicTarihi,
                BitisTarihi = assignment.BitisTarihi,
                AyrilanKisiSayisi = assignment.AyrilanKisiSayisi,
                MevcutOdaId = assignment.OdaId,
                MevcutOdaNo = assignment.OdaNo,
                MevcutBinaAdi = assignment.BinaAdi,
                MevcutOdaTipiAdi = assignment.OdaTipiAdi,
                MevcutOdaPaylasimliMi = assignment.PaylasimliMi,
                MevcutOdaKapasitesi = assignment.Kapasite,
                ProblemliMi = true,
                TasinacakKonaklayanlar = guestAssignments
                    .Where(x => x.RezervasyonSegmentId == assignment.SegmentId && x.OdaId == assignment.OdaId)
                    .OrderBy(x => x.SiraNo)
                    .Select(x => new RezervasyonOdaDegisimKonaklayanDto
                    {
                        SiraNo = x.SiraNo,
                        AdSoyad = x.AdSoyad,
                        MevcutYatakNo = x.YatakNo
                    })
                    .ToList(),
                AdayOdalar = adayOdalar
            });
        }

        return new RezervasyonOdaDegisimSecenekDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            Kayitlar = kayitlar
                .OrderBy(x => x.SegmentSirasi)
                .ThenBy(x => x.RezervasyonSegmentOdaAtamaId)
                .ToList()
        };
    }

    public async Task<RezervasyonKayitSonucDto> KaydetOdaDegisimiAsync(int rezervasyonId, RezervasyonOdaDegisimKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Atamalar.Count == 0)
        {
            throw new BaseException("Kaydedilecek oda degisimi bulunamadi.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        EnsureCanChangeRoomForReservationStatus(reservation.RezervasyonDurumu);

        var assignments = await _stysDbContext.RezervasyonSegmentOdaAtamalari
            .Include(x => x.RezervasyonSegment)
            .Where(x => x.RezervasyonSegment != null && x.RezervasyonSegment.RezervasyonId == rezervasyonId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            throw new BaseException("Rezervasyon segment/oda atama kaydi bulunamadi.", 400);
        }

        var requestMap = request.Atamalar
            .GroupBy(x => x.RezervasyonSegmentOdaAtamaId)
            .ToDictionary(x => x.Key, x => x.Last().YeniOdaId);

        if (requestMap.Keys.Any(x => x <= 0) || requestMap.Values.Any(x => x <= 0))
        {
            throw new BaseException("Gecersiz oda degisimi talebi.", 400);
        }

        var assignmentById = assignments.ToDictionary(x => x.Id);
        if (requestMap.Keys.Any(x => !assignmentById.ContainsKey(x)))
        {
            throw new BaseException("Degisimi istenen segment oda atamasi bulunamadi.", 404);
        }

        var changedAssignments = requestMap
            .Where(x => assignmentById[x.Key].OdaId != x.Value)
            .Select(x => new
            {
                AtamaId = x.Key,
                YeniOdaId = x.Value
            })
            .ToList();
        var changedAssignmentIdSet = changedAssignments
            .Select(x => x.AtamaId)
            .ToHashSet();

        if (changedAssignments.Count == 0)
        {
            return ToSaveResult(reservation);
        }

        var changedAssignmentContexts = changedAssignments
            .Select(x =>
            {
                var assignment = assignmentById[x.AtamaId];
                return new OdaDegisimAppliedChange(
                    assignment.Id,
                    assignment.RezervasyonSegmentId,
                    assignment.RezervasyonSegment?.SegmentSirasi ?? 0,
                    assignment.RezervasyonSegment?.BaslangicTarihi ?? reservation.GirisTarihi,
                    assignment.RezervasyonSegment?.BitisTarihi ?? reservation.CikisTarihi,
                    assignment.OdaId,
                    x.YeniOdaId);
            })
            .ToList();

        var oldAssignmentsSnapshot = changedAssignments
            .Select(x =>
            {
                var assignment = assignmentById[x.AtamaId];
                return new
                {
                    RezervasyonSegmentOdaAtamaId = assignment.Id,
                    assignment.RezervasyonSegmentId,
                    SegmentSirasi = assignment.RezervasyonSegment?.SegmentSirasi ?? 0,
                    assignment.OdaId,
                    assignment.OdaNoSnapshot,
                    assignment.BinaAdiSnapshot,
                    assignment.OdaTipiAdiSnapshot,
                    assignment.AyrilanKisiSayisi,
                    assignment.KapasiteSnapshot,
                    assignment.PaylasimliMiSnapshot
                };
            })
            .OrderBy(x => x.RezervasyonSegmentId)
            .ThenBy(x => x.RezervasyonSegmentOdaAtamaId)
            .ToList();

        var finalRoomByAssignmentId = assignments.ToDictionary(x => x.Id, x => x.OdaId);
        foreach (var changed in changedAssignments)
        {
            finalRoomByAssignmentId[changed.AtamaId] = changed.YeniOdaId;
        }

        var assignmentsBySegment = assignments
            .GroupBy(x => x.RezervasyonSegmentId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var segmentGroup in assignmentsBySegment)
        {
            var selectedRoomIds = segmentGroup.Value
                .Select(x => finalRoomByAssignmentId[x.Id])
                .ToList();

            if (selectedRoomIds.Distinct().Count() != selectedRoomIds.Count)
            {
                throw new BaseException("Ayni segmentte ayni oda birden fazla kez secilemez.", 400);
            }
        }

        var allSelectedRoomIds = finalRoomByAssignmentId.Values
            .Distinct()
            .ToList();

        var selectedRooms = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join roomType in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals roomType.Id
                where oda.AktifMi
                      && bina.AktifMi
                      && roomType.AktifMi
                      && bina.TesisId == reservation.TesisId
                      && allSelectedRoomIds.Contains(oda.Id)
                select new OdaDegisimRoomInfo(
                    oda.Id,
                    oda.OdaNo,
                    bina.Ad,
                    roomType.Ad,
                    roomType.Kapasite,
                    roomType.PaylasimliMi))
            .ToListAsync(cancellationToken);

        if (selectedRooms.Count != allSelectedRoomIds.Count)
        {
            throw new BaseException("Secilen odalardan en az biri gecersiz veya rezervasyon tesisi disinda.", 400);
        }

        var roomInfoById = selectedRooms.ToDictionary(x => x.OdaId);
        var reservationGuestAssignments = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                where konaklayan.RezervasyonId == rezervasyonId
                select new
                {
                    atama.RezervasyonSegmentId,
                    atama.OdaId,
                    konaklayan.Cinsiyet
                })
            .ToListAsync(cancellationToken);

        foreach (var segmentGroup in assignmentsBySegment.Values)
        {
            var segment = segmentGroup[0].RezervasyonSegment
                          ?? throw new BaseException("Rezervasyon segment bilgisi eksik.", 400);

            var selectedRoomIds = segmentGroup
                .Select(x => finalRoomByAssignmentId[x.Id])
                .Distinct()
                .ToList();

            var blockedRoomIds = await _stysDbContext.OdaKullanimBloklari
                .Where(x =>
                    x.AktifMi
                    && selectedRoomIds.Contains(x.OdaId)
                    && x.BaslangicTarihi < segment.BitisTarihi
                    && x.BitisTarihi > segment.BaslangicTarihi)
                .Select(x => x.OdaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (blockedRoomIds.Count > 0)
            {
                var blockedRoomId = blockedRoomIds[0];
                var blockedRoomNo = roomInfoById.TryGetValue(blockedRoomId, out var info)
                    ? info.OdaNo
                    : blockedRoomId.ToString();
                throw new BaseException($"'{blockedRoomNo}' odasi icin secilen aralikta bakim/ariza kaydi mevcut.", 400);
            }

            var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
                selectedRoomIds,
                segment.BaslangicTarihi,
                segment.BitisTarihi,
                cancellationToken,
                rezervasyonId);
            var externalSharedRoomOccupancies = await GetSharedRoomGuestOccupanciesAsync(
                selectedRoomIds,
                segment.BaslangicTarihi,
                segment.BitisTarihi,
                cancellationToken,
                rezervasyonId);

            foreach (var assignment in segmentGroup)
            {
                var roomId = finalRoomByAssignmentId[assignment.Id];
                var roomInfo = roomInfoById[roomId];
                var occupied = occupancyByRoom.TryGetValue(roomId, out var value) ? value : 0;
                var assigned = assignment.AyrilanKisiSayisi;

                if (!roomInfo.PaylasimliMi)
                {
                    if (occupied > 0)
                    {
                        throw new BaseException($"'{roomInfo.OdaNo}' odasi secilen tarih araliginda musait degil.", 400);
                    }

                    if (assigned > roomInfo.Kapasite)
                    {
                        throw new BaseException($"'{roomInfo.OdaNo}' odasi icin ayrilan kisi sayisi kapasiteyi asiyor.", 400);
                    }

                    continue;
                }

                if (changedAssignmentIdSet.Contains(assignment.Id))
                {
                    var movedGuestGenders = reservationGuestAssignments
                        .Where(x => x.RezervasyonSegmentId == assignment.RezervasyonSegmentId && x.OdaId == assignment.OdaId)
                        .Select(x => x.Cinsiyet)
                        .ToList();
                    string? movingGuestGender = null;
                    if (movedGuestGenders.Count > 0 && !TryResolveSharedRoomGuestGender(movedGuestGenders, out movingGuestGender))
                    {
                        throw new BaseException("Paylasimli odaya tasinacak tum konaklayanlar icin cinsiyet bilgisi zorunludur ve ayni olmalidir.", 400);
                    }

                    if (movedGuestGenders.Count > 0)
                    {
                        var roomGenderSet = GetDistinctSharedRoomGenders(
                            externalSharedRoomOccupancies,
                            roomId,
                            segment.BaslangicTarihi,
                            segment.BitisTarihi);
                        if (roomGenderSet.Count > 1)
                        {
                            throw new BaseException($"'{roomInfo.OdaNo}' odasi icin mevcut cinsiyet dagilimi tutarsiz.", 400);
                        }

                        if (roomGenderSet.Count == 1
                            && !string.Equals(roomGenderSet.Single(), movingGuestGender, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new BaseException($"'{roomInfo.OdaNo}' paylasimli odasina farkli cinsiyetten konaklayan tasinamaz.", 400);
                        }
                    }
                }

                var remaining = Math.Max(0, roomInfo.Kapasite - occupied);
                if (assigned > remaining)
                {
                    throw new BaseException($"'{roomInfo.OdaNo}' odasi icin secilen aralikta yeterli kapasite yok.", 400);
                }
            }
        }

        foreach (var changed in changedAssignments)
        {
            var assignment = assignmentById[changed.AtamaId];
            var selectedRoom = roomInfoById[changed.YeniOdaId];
            assignment.OdaId = selectedRoom.OdaId;
            assignment.OdaNoSnapshot = selectedRoom.OdaNo;
            assignment.BinaAdiSnapshot = selectedRoom.BinaAdi;
            assignment.OdaTipiAdiSnapshot = selectedRoom.OdaTipiAdi;
            assignment.PaylasimliMiSnapshot = selectedRoom.PaylasimliMi;
            assignment.KapasiteSnapshot = selectedRoom.Kapasite;
        }

        await ReassignGuestSegmentAssignmentsAfterRoomChangeAsync(
            rezervasyonId,
            changedAssignmentContexts,
            roomInfoById,
            cancellationToken);

        await TryApplyPriceDecreaseAfterRoomChangeAsync(
            reservation,
            assignmentsBySegment,
            finalRoomByAssignmentId,
            cancellationToken);

        var newAssignmentsSnapshot = changedAssignments
            .Select(x =>
            {
                var assignment = assignmentById[x.AtamaId];
                return new
                {
                    RezervasyonSegmentOdaAtamaId = assignment.Id,
                    assignment.RezervasyonSegmentId,
                    SegmentSirasi = assignment.RezervasyonSegment?.SegmentSirasi ?? 0,
                    assignment.OdaId,
                    assignment.OdaNoSnapshot,
                    assignment.BinaAdiSnapshot,
                    assignment.OdaTipiAdiSnapshot,
                    assignment.AyrilanKisiSayisi,
                    assignment.KapasiteSnapshot,
                    assignment.PaylasimliMiSnapshot
                };
            })
            .OrderBy(x => x.RezervasyonSegmentId)
            .ThenBy(x => x.RezervasyonSegmentOdaAtamaId)
            .ToList();

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.OdaDegisimiYapildi,
            $"{changedAssignments.Count} segment oda atamasi degistirildi.",
            oldAssignmentsSnapshot,
            newAssignmentsSnapshot);

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            reservation.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "OdaDegisimi",
                Baslik = "Rezervasyonda Oda Degisimi",
                Mesaj = $"{reservation.ReferansNo} rezervasyonu icin {changedAssignments.Count} segmentte oda degisimi kaydedildi.",
                Severity = BildirimSeverityleri.Warn,
                Link = "/rezervasyon-yonetimi"
            },
            cancellationToken);

        return ToSaveResult(reservation);
    }

    public async Task<List<UygunOdaDto>> GetUygunOdalarAsync(UygunOdaAramaRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);
        await EnsureSeasonRuleComplianceAsync(request.TesisId, request.BaslangicTarihi, request.BitisTarihi, cancellationToken);

        if (request.OdaTipiId.HasValue && request.OdaTipiId.Value > 0)
        {
            var odaTipi = await _stysDbContext.OdaTipleri
                .Where(x => x.Id == request.OdaTipiId.Value && x.AktifMi)
                .Select(x => new { x.Id, x.TesisId })
                .FirstOrDefaultAsync(cancellationToken);
            if (odaTipi is null || odaTipi.TesisId != request.TesisId)
            {
                throw new BaseException("Secilen oda tipi, tesis ile uyumlu degil.", 400);
            }
        }

        var baslangic = request.BaslangicTarihi;
        var bitis = request.BitisTarihi;

        var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
            (await _stysDbContext.Odalar
                .Where(x => x.AktifMi)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)),
            baslangic,
            bitis,
            cancellationToken);

        var uygunOdalarQuery =
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
                  && bina.TesisId == request.TesisId
                  && odaTipi.Kapasite >= request.KisiSayisi
                  && (!request.OdaTipiId.HasValue || request.OdaTipiId.Value <= 0 || oda.TesisOdaTipiId == request.OdaTipiId.Value)
            select new UygunOdaDto
            {
                OdaId = oda.Id,
                OdaNo = oda.OdaNo,
                BinaId = bina.Id,
                BinaAdi = bina.Ad,
                OdaTipiId = odaTipi.Id,
                OdaTipiAdi = odaTipi.Ad,
                Kapasite = odaTipi.Kapasite,
                PaylasimliMi = odaTipi.PaylasimliMi
            };

        var suitableRooms = await uygunOdalarQuery
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ThenBy(x => x.OdaId)
            .ToListAsync(cancellationToken);

        return suitableRooms.Where(room =>
        {
            var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;
            if (!room.PaylasimliMi)
            {
                return occupied == 0;
            }

            return room.Kapasite - occupied >= request.KisiSayisi;
        }).ToList();
    }

    public async Task<List<KonaklamaSenaryoDto>> GetKonaklamaSenaryolariAsync(KonaklamaSenaryoAramaRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateScenarioRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);
        await EnsureTesisHasMisafirTipiAsync(request.TesisId, request.MisafirTipiId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(request.TesisId, request.KonaklamaTipiId, cancellationToken);
        await EnsureSeasonRuleComplianceAsync(request.TesisId, request.BaslangicTarihi, request.BitisTarihi, cancellationToken);
        var guestGenderRequirements = BuildScenarioGuestGenderRequirements(request.KonaklayanCinsiyetleri, request.KisiSayisi);

        var scenarios = new List<KonaklamaSenaryoDto>();

        var fullIntervalAvailabilities = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            guestGenderRequirements,
            request.BaslangicTarihi,
            request.BitisTarihi,
            cancellationToken);

        var fullIntervalVariants = BuildSingleSegmentVariants(
            request.KisiSayisi,
            guestGenderRequirements,
            request.BaslangicTarihi,
            request.BitisTarihi,
            fullIntervalAvailabilities);

        scenarios.AddRange(fullIntervalVariants);

        if (request.BitisTarihi - request.BaslangicTarihi > TimeSpan.FromHours(6))
        {
            var segmentedScenario = await BuildTwoSegmentScenarioAsync(request, guestGenderRequirements, cancellationToken);
            if (segmentedScenario is not null)
            {
                scenarios.Add(segmentedScenario);
            }
        }

        var distinct = scenarios
            .GroupBy(CreateScenarioKey)
            .Select(group => group.First())
            .ToList();

        foreach (var scenario in distinct)
        {
            var pricing = await CalculateScenarioPriceAsync(
                request.TesisId,
                request.MisafirTipiId,
                request.KonaklamaTipiId,
                request.KisiSayisi,
                request.TekKisilikFiyatUygulansinMi,
                request.BaslangicTarihi,
                request.BitisTarihi,
                scenario.Segmentler.Select(x => new SenaryoFiyatHesaplaSegmentDto
                {
                    BaslangicTarihi = x.BaslangicTarihi,
                    BitisTarihi = x.BitisTarihi,
                    OdaAtamalari = x.OdaAtamalari.Select(y => new SenaryoFiyatHesaplaOdaAtamaDto
                    {
                        OdaId = y.OdaId,
                        AyrilanKisiSayisi = y.AyrilanKisiSayisi
                    }).ToList()
                }).ToList(),
                [],
                cancellationToken);

            scenario.ToplamBazUcret = pricing.ToplamBazUcret;
            scenario.ToplamNihaiUcret = pricing.ToplamNihaiUcret;
            scenario.ParaBirimi = pricing.ParaBirimi;
        }

        var sortedByPrice = distinct
            .OrderBy(x => x.ToplamNihaiUcret)
            .ThenBy(x => x.ToplamBazUcret)
            .ThenBy(x => x.ToplamOdaSayisi)
            .ThenBy(x => x.OdaDegisimSayisi)
            .Take(5)
            .ToList();

        for (var i = 0; i < sortedByPrice.Count; i++)
        {
            sortedByPrice[i].SenaryoKodu = $"SENARYO-{i + 1}";
        }

        return sortedByPrice;
    }

    public Task<SenaryoFiyatHesaplamaSonucuDto> HesaplaSenaryoFiyatiAsync(SenaryoFiyatHesaplaRequestDto request, CancellationToken cancellationToken = default)
    {
        return CalculateScenarioPriceAsync(
            request.TesisId,
            request.MisafirTipiId,
            request.KonaklamaTipiId,
            request.KisiSayisi,
            request.TekKisilikFiyatUygulansinMi,
            request.BaslangicTarihi,
            request.BitisTarihi,
            request.Segmentler,
            request.SeciliIndirimKuraliIds,
            cancellationToken);
    }

    public async Task<RezervasyonKayitSonucDto> KaydetAsync(RezervasyonKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateSaveRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);
        await EnsureTesisHasMisafirTipiAsync(request.TesisId, request.MisafirTipiId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(request.TesisId, request.KonaklamaTipiId, cancellationToken);
        await EnsureSeasonRuleComplianceAsync(request.TesisId, request.GirisTarihi, request.CikisTarihi, cancellationToken);
        await ValidateAppliedDiscountPermissionsAsync(request, cancellationToken);

        var distinctRoomIds = request.Segmentler
            .SelectMany(x => x.OdaAtamalari)
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        // Ayni odalara eszamanli rezervasyon yazimlarinda race condition olmamasi icin
        // DB transaction + applock ile kritik bolgeyi serialize ediyoruz.
        await using var transaction = await _stysDbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await AcquireRoomApplicationLocksAsync(distinctRoomIds, cancellationToken);

        var rooms = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where distinctRoomIds.Contains(oda.Id)
                  && oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
            select new RoomInfo(
                oda.Id,
                oda.OdaNo,
                bina.TesisId,
                bina.Ad,
                odaTipi.Ad,
                odaTipi.Kapasite,
                odaTipi.PaylasimliMi))
            .ToListAsync(cancellationToken);

        if (rooms.Count != distinctRoomIds.Count || rooms.Any(x => x.TesisId != request.TesisId))
        {
            throw new BaseException("Secilen odalardan en az biri gecersiz veya secilen tesise ait degil.", 400);
        }

        foreach (var segment in request.Segmentler)
        {
            var segmentRoomIds = segment.OdaAtamalari
                .Select(x => x.OdaId)
                .Distinct()
                .ToList();

            var blockedRoomIds = await _stysDbContext.OdaKullanimBloklari
                .Where(x =>
                    x.AktifMi
                    && segmentRoomIds.Contains(x.OdaId)
                    && x.BaslangicTarihi < segment.BitisTarihi
                    && x.BitisTarihi > segment.BaslangicTarihi)
                .Select(x => x.OdaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (blockedRoomIds.Count > 0)
            {
                var blockedRoom = rooms.FirstOrDefault(x => x.OdaId == blockedRoomIds[0]);
                var blockedRoomNo = blockedRoom?.OdaNo ?? blockedRoomIds[0].ToString();
                throw new BaseException($"'{blockedRoomNo}' odasi icin secilen aralikta bakim/ariza kaydi mevcut.", 400);
            }

            var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
                segmentRoomIds,
                segment.BaslangicTarihi,
                segment.BitisTarihi,
                cancellationToken);

            var assignedByRoom = segment.OdaAtamalari
                .GroupBy(x => x.OdaId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.AyrilanKisiSayisi));

            foreach (var roomAssignment in assignedByRoom)
            {
                var room = rooms.First(x => x.OdaId == roomAssignment.Key);
                var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;

                var remainingCapacity = room.PaylasimliMi
                    ? Math.Max(0, room.Kapasite - occupied)
                    : occupied > 0
                        ? 0
                        : room.Kapasite;

                if (roomAssignment.Value > remainingCapacity)
                {
                    throw new BaseException($"'{room.OdaNo}' odasi icin secilen aralikta yeterli kapasite yok.", 400);
                }
            }
        }

        var reservation = new Entities.Rezervasyon
        {
            ReferansNo = GenerateReferenceNo(),
            TesisId = request.TesisId,
            KisiSayisi = request.KisiSayisi,
            MisafirTipiId = request.MisafirTipiId,
            KonaklamaTipiId = request.KonaklamaTipiId,
            GirisTarihi = request.GirisTarihi,
            CikisTarihi = request.CikisTarihi,
            TekKisilikFiyatUygulandiMi = request.TekKisilikFiyatUygulansinMi,
            MisafirAdiSoyadi = request.MisafirAdiSoyadi.Trim(),
            MisafirTelefon = request.MisafirTelefon.Trim(),
            MisafirEposta = string.IsNullOrWhiteSpace(request.MisafirEposta) ? null : request.MisafirEposta.Trim(),
            TcKimlikNo = string.IsNullOrWhiteSpace(request.TcKimlikNo) ? null : request.TcKimlikNo.Trim(),
            PasaportNo = string.IsNullOrWhiteSpace(request.PasaportNo) ? null : request.PasaportNo.Trim(),
            MisafirCinsiyeti = NormalizeKonaklayanCinsiyet(request.MisafirCinsiyeti),
            Notlar = string.IsNullOrWhiteSpace(request.Notlar) ? null : request.Notlar.Trim(),
            ToplamBazUcret = request.ToplamBazUcret > 0 ? request.ToplamBazUcret : request.ToplamUcret,
            ToplamUcret = request.ToplamUcret,
            ParaBirimi = string.IsNullOrWhiteSpace(request.ParaBirimi) ? "TRY" : request.ParaBirimi.Trim().ToUpperInvariant(),
            UygulananIndirimlerJson = SerializeAppliedDiscounts(request.UygulananIndirimler),
            RezervasyonDurumu = RezervasyonDurumlari.Taslak,
            AktifMi = true
        };

        var orderedSegments = request.Segmentler
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.BitisTarihi)
            .ToList();

        for (var i = 0; i < orderedSegments.Count; i++)
        {
            var segmentRequest = orderedSegments[i];
            var segment = new Entities.RezervasyonSegment
            {
                SegmentSirasi = i + 1,
                BaslangicTarihi = segmentRequest.BaslangicTarihi,
                BitisTarihi = segmentRequest.BitisTarihi
            };

            foreach (var odaAtamaRequest in segmentRequest.OdaAtamalari)
            {
                var room = rooms.First(x => x.OdaId == odaAtamaRequest.OdaId);
                segment.OdaAtamalari.Add(new Entities.RezervasyonSegmentOdaAtama
                {
                    OdaId = room.OdaId,
                    AyrilanKisiSayisi = odaAtamaRequest.AyrilanKisiSayisi,
                    OdaNoSnapshot = room.OdaNo,
                    BinaAdiSnapshot = room.BinaAdi,
                    OdaTipiAdiSnapshot = room.OdaTipiAdi,
                    PaylasimliMiSnapshot = room.PaylasimliMi,
                    KapasiteSnapshot = room.Kapasite
                });
            }

            reservation.Segmentler.Add(segment);
        }

        var konaklamaHaklari = await BuildKonaklamaHaklariAsync(
            reservation.TesisId,
            request.KonaklamaTipiId,
            reservation.GirisTarihi,
            reservation.CikisTarihi,
            cancellationToken);

        foreach (var hak in konaklamaHaklari)
        {
            reservation.KonaklamaHaklari.Add(hak);
        }

        AppendHistoryEntry(
            reservation,
            RezervasyonGecmisIslemTipleri.RezervasyonOlusturuldu,
            "Rezervasyon olusturuldu.",
            null,
            new
            {
                reservation.ReferansNo,
                reservation.TesisId,
                reservation.KisiSayisi,
                reservation.GirisTarihi,
                reservation.CikisTarihi,
                reservation.ToplamBazUcret,
                reservation.ToplamUcret,
                SegmentSayisi = reservation.Segmentler.Count,
                OdaAtamaSayisi = reservation.Segmentler.Sum(x => x.OdaAtamalari.Count),
                KonaklamaHakSayisi = reservation.KonaklamaHaklari.Count,
                reservation.RezervasyonDurumu
            });

        if (reservation.KonaklamaHaklari.Count > 0)
        {
            AppendHistoryEntry(
                reservation,
                RezervasyonGecmisIslemTipleri.KonaklamaHaklariUretildi,
                "Konaklama tipine bagli rezervasyon haklari uretildi.",
                null,
                reservation.KonaklamaHaklari
                    .OrderBy(x => x.HakTarihi)
                    .ThenBy(x => x.HizmetAdiSnapshot)
                    .Select(x => new
                    {
                        x.HizmetKodu,
                        HizmetAdi = x.HizmetAdiSnapshot,
                        x.Miktar,
                        x.Periyot,
                        PeriyotAdi = x.PeriyotAdiSnapshot,
                        x.HakTarihi,
                        x.Durum
                    })
                    .ToList());
        }

        await _stysDbContext.Rezervasyonlar.AddAsync(reservation, cancellationToken);
        await _stysDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToSaveResult(reservation);
    }

    public async Task<RezervasyonKayitSonucDto> TamamlaCheckInAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.CheckInTamamlandi)
        {
            return ToSaveResult(reservation);
        }

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi)
        {
            throw new BaseException("Check-out tamamlanmis rezervasyon icin check-in yapilamaz.", 400);
        }

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyon icin check-in yapilamaz.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.Taslak
            && reservation.RezervasyonDurumu != RezervasyonDurumlari.Onayli)
        {
            throw new BaseException("Bu rezervasyon durumu icin check-in islemi yapilamaz.", 400);
        }

        var segmentCount = await _stysDbContext.RezervasyonSegmentleri
            .Where(x => x.RezervasyonId == rezervasyonId)
            .CountAsync(cancellationToken);

        if (segmentCount <= 0)
        {
            throw new BaseException("Check-in icin rezervasyon segmentleri bulunamadi.", 400);
        }

        var guestInfos = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId)
            .Select(x => new
            {
                x.AdSoyad,
                x.KatilimDurumu,
                AtamaCount = x.SegmentAtamalari.Count
            })
            .ToListAsync(cancellationToken);

        if (guestInfos.Count != reservation.KisiSayisi)
        {
            throw new BaseException("Check-in icin tum konaklayanlarin plani tamamlanmalidir.", 400);
        }

        if (guestInfos.Any(x => string.IsNullOrWhiteSpace(x.AdSoyad)))
        {
            throw new BaseException("Check-in icin konaklayan ad soyad bilgileri zorunludur.", 400);
        }

        var normalizedGuestInfos = guestInfos
            .Select(x => new
            {
                x.AdSoyad,
                KatilimDurumu = NormalizeKonaklayanKatilimDurumu(x.KatilimDurumu),
                x.AtamaCount
            })
            .ToList();

        if (normalizedGuestInfos.Any(x => x.KatilimDurumu == KonaklayanKatilimDurumlari.Ayrildi))
        {
            throw new BaseException("Check-in oncesi Ayrildi durumunda konaklayan olamaz.", 400);
        }

        if (normalizedGuestInfos.All(x => x.KatilimDurumu != KonaklayanKatilimDurumlari.Geldi))
        {
            throw new BaseException("Check-in icin en az bir konaklayan Geldi olarak isaretlenmelidir.", 400);
        }

        if (normalizedGuestInfos.Any(x => DoesGuestRequireAssignments(x.KatilimDurumu) && x.AtamaCount != segmentCount))
        {
            throw new BaseException("Check-in icin gelen veya beklenen konaklayanlarin tum segment atamalari tamamlanmalidir.", 400);
        }

        await EnsureNoActiveRoomBlockForReservationAsync(rezervasyonId, cancellationToken);
        await EnsureRoomsReadyForCheckInAsync(rezervasyonId, cancellationToken);

        var previousStatus = reservation.RezervasyonDurumu;
        reservation.RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi;
        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.CheckInTamamlandi,
            "Check-in islemi tamamlandi.",
            new { RezervasyonDurumu = previousStatus },
            new { RezervasyonDurumu = reservation.RezervasyonDurumu });
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            reservation.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "CheckIn",
                Baslik = "Check-in Tamamlandi",
                Mesaj = $"{reservation.ReferansNo} referansli rezervasyon icin check-in islemi tamamlandi.",
                Severity = BildirimSeverityleri.Success,
                Link = "/rezervasyon-yonetimi"
            },
            cancellationToken);

        return ToSaveResult(reservation);
    }

    public async Task<RezervasyonCheckInKontrolDto> GetCheckInKontrolAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        var warnings = await GetCheckInWarningsAsync(rezervasyonId, cancellationToken);
        var hasBlockingWarnings = warnings.Any(x => x.EngelleyiciMi);

        return new RezervasyonCheckInKontrolDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            CheckInYapilabilir = !hasBlockingWarnings,
            Uyarilar = warnings
        };
    }

    public async Task<RezervasyonKayitSonucDto> TamamlaCheckOutAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi)
        {
            return ToSaveResult(reservation);
        }

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyon icin check-out yapilamaz.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Check-out icin once check-in tamamlanmalidir.", 400);
        }

        var odenenTutar = await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == reservation.Id)
            .Select(x => (decimal?)x.OdemeTutari)
            .SumAsync(cancellationToken) ?? 0m;

        var ekHizmetToplami = await GetRezervasyonEkHizmetToplamiAsync(rezervasyonId, cancellationToken);
        var kalanTutar = (reservation.ToplamUcret + ekHizmetToplami) - odenenTutar;
        if (kalanTutar > 0m)
        {
            throw new BaseException("Check-out icin once kalan odeme tamamlanmalidir.", 400);
        }

        var konaklayanDurumlari = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId)
            .ToListAsync(cancellationToken);

        if (konaklayanDurumlari.Any(x => NormalizeKonaklayanKatilimDurumu(x.KatilimDurumu) == KonaklayanKatilimDurumlari.Bekleniyor))
        {
            throw new BaseException("Check-out icin bekleyen konaklayanlar netlestirilmelidir.", 400);
        }

        foreach (var konaklayan in konaklayanDurumlari
                     .Where(x => NormalizeKonaklayanKatilimDurumu(x.KatilimDurumu) == KonaklayanKatilimDurumlari.Geldi))
        {
            konaklayan.KatilimDurumu = KonaklayanKatilimDurumlari.Ayrildi;
        }

        var previousStatus = reservation.RezervasyonDurumu;
        reservation.RezervasyonDurumu = RezervasyonDurumlari.CheckOutTamamlandi;
        await MarkReservationRoomsAsDirtyAsync(reservation.Id, cancellationToken);
        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.CheckOutTamamlandi,
            "Check-out islemi tamamlandi.",
            new { RezervasyonDurumu = previousStatus },
            new { RezervasyonDurumu = reservation.RezervasyonDurumu });
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            reservation.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "CheckOut",
                Baslik = "Check-out Tamamlandi",
                Mesaj = $"{reservation.ReferansNo} referansli rezervasyon icin check-out tamamlandi. Ilgili odalar kirli durumuna alindi.",
                Severity = BildirimSeverityleri.Info,
                Link = "/rezervasyon-yonetimi"
            },
            cancellationToken);

        return ToSaveResult(reservation);
    }

    private async Task MarkReservationRoomsAsDirtyAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var roomIds = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                where segment.RezervasyonId == rezervasyonId
                select atama.OdaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (roomIds.Count == 0)
        {
            return;
        }

        var rooms = await _stysDbContext.Odalar
            .Where(x => roomIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var room in rooms)
        {
            room.TemizlikDurumu = OdaTemizlikDurumlari.Kirli;
        }
    }

    private async Task EnsureRoomsReadyForCheckInAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var blockingWarnings = (await GetCheckInWarningsAsync(rezervasyonId, cancellationToken))
            .Where(x => x.EngelleyiciMi && x.OdaId > 0)
            .ToList();

        if (blockingWarnings.Count == 0)
        {
            return;
        }

        var roomSummary = string.Join(", ", blockingWarnings
            .Select(x => $"{x.OdaNo} - {x.BinaAdi} ({x.TemizlikDurumu})"));

        var reservationInfo = await _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId)
            .Select(x => new { x.TesisId, x.ReferansNo })
            .FirstOrDefaultAsync(cancellationToken);

        if (reservationInfo is not null)
        {
            await _bildirimService.PublishToTesisUsersAsync(
                reservationInfo.TesisId,
                new BildirimOlusturRequestDto
                {
                    Tip = "TemizlikGecikmesi",
                    Baslik = "Check-in Temizlik Nedeniyle Engellendi",
                    Mesaj = $"{reservationInfo.ReferansNo} rezervasyonu icin hazir olmayan oda(lar) var: {roomSummary}.",
                    Severity = BildirimSeverityleri.Warn,
                    Link = "/oda-temizlik-yonetimi"
                },
                cancellationToken);
        }

        throw new BaseException(
            $"Check-in engellendi. Hazir olmayan odalar var: {roomSummary}.",
            400);
    }

    private async Task<List<RezervasyonCheckInUyariDto>> GetCheckInWarningsAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var reservationInfo = await _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId)
            .Select(x => new
            {
                x.KisiSayisi,
                SegmentCount = x.Segmentler.Count,
                Konaklayanlar = x.Konaklayanlar
                    .Select(k => new
                    {
                        k.AdSoyad,
                        k.KatilimDurumu,
                        AtamaCount = k.SegmentAtamalari.Count
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (reservationInfo is null)
        {
            return [];
        }

        var warnings = new List<RezervasyonCheckInUyariDto>();
        var normalizedGuests = reservationInfo.Konaklayanlar
            .Select(x => new
            {
                x.AdSoyad,
                KatilimDurumu = NormalizeKonaklayanKatilimDurumu(x.KatilimDurumu),
                x.AtamaCount
            })
            .ToList();

        if (normalizedGuests.Count != reservationInfo.KisiSayisi)
        {
            warnings.Add(new RezervasyonCheckInUyariDto
            {
                Mesaj = "Check-in icin tum konaklayanlarin plani tamamlanmalidir.",
                EngelleyiciMi = true
            });
        }

        if (normalizedGuests.Any(x => string.IsNullOrWhiteSpace(x.AdSoyad)))
        {
            warnings.Add(new RezervasyonCheckInUyariDto
            {
                Mesaj = "Check-in icin konaklayan ad soyad bilgileri zorunludur.",
                EngelleyiciMi = true
            });
        }

        if (normalizedGuests.Any(x => x.KatilimDurumu == KonaklayanKatilimDurumlari.Ayrildi))
        {
            warnings.Add(new RezervasyonCheckInUyariDto
            {
                Mesaj = "Check-in oncesi Ayrildi durumunda konaklayan olamaz.",
                EngelleyiciMi = true
            });
        }

        if (normalizedGuests.Count > 0 && normalizedGuests.All(x => x.KatilimDurumu != KonaklayanKatilimDurumlari.Geldi))
        {
            warnings.Add(new RezervasyonCheckInUyariDto
            {
                Mesaj = "Check-in icin en az bir konaklayan Geldi olarak isaretlenmelidir.",
                EngelleyiciMi = true
            });
        }

        if (reservationInfo.SegmentCount > 0
            && normalizedGuests.Any(x => DoesGuestRequireAssignments(x.KatilimDurumu) && x.AtamaCount != reservationInfo.SegmentCount))
        {
            warnings.Add(new RezervasyonCheckInUyariDto
            {
                Mesaj = "Check-in icin gelen veya beklenen konaklayanlarin tum segment atamalari tamamlanmalidir.",
                EngelleyiciMi = true
            });
        }

        var roomInfos = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                join oda in _stysDbContext.Odalar on atama.OdaId equals oda.Id
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                where segment.RezervasyonId == rezervasyonId
                select new
                {
                    OdaId = oda.Id,
                    OdaNo = atama.OdaNoSnapshot,
                    BinaAdi = atama.BinaAdiSnapshot,
                    oda.TemizlikDurumu
                })
            .ToListAsync(cancellationToken);

        warnings.AddRange(roomInfos
            .GroupBy(x => x.OdaId)
            .Select(group => group.First())
            .Where(x => !string.Equals(x.TemizlikDurumu, OdaTemizlikDurumlari.Hazir, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ThenBy(x => x.OdaId)
            .Select(x => new RezervasyonCheckInUyariDto
            {
                OdaId = x.OdaId,
                OdaNo = x.OdaNo,
                BinaAdi = x.BinaAdi,
                TemizlikDurumu = x.TemizlikDurumu,
                Mesaj = "Oda check-in icin hazir degil.",
                EngelleyiciMi = true
            })
            .ToList());

        return warnings;
    }

    public async Task<RezervasyonKayitSonucDto> IptalEtAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            var ownRoomIds = await _stysDbContext.RezervasyonSegmentOdaAtamalari
                .Where(x => x.RezervasyonSegment != null && x.RezervasyonSegment.RezervasyonId == reservation.Id)
                .Select(x => x.OdaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Iptal geri alma sirasinda da ayni odalara paralel rezervasyon yazimlarini serialize et.
            await using var transaction = await _stysDbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await AcquireRoomApplicationLocksAsync(ownRoomIds, cancellationToken);

            await EnsureCanRevertCancellationAsync(reservation.Id, cancellationToken);
            var previousStatus = reservation.RezervasyonDurumu;
            reservation.RezervasyonDurumu = RezervasyonDurumlari.Taslak;
            await SetKonaklamaHakDurumuAsync(reservation.Id, RezervasyonKonaklamaHakDurumlari.Iptal, RezervasyonKonaklamaHakDurumlari.Bekliyor, cancellationToken);
            AppendHistoryEntry(
                reservation.Id,
                RezervasyonGecmisIslemTipleri.IptalGeriAlindi,
                "Rezervasyon iptali geri alindi.",
                new { RezervasyonDurumu = previousStatus },
                new { RezervasyonDurumu = reservation.RezervasyonDurumu });
            await _stysDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToSaveResult(reservation);
        }

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi)
        {
            throw new BaseException("Check-out tamamlanmis rezervasyon iptal edilemez.", 400);
        }

        var odenenTutar = await GetToplamOdenenTutarAsync(rezervasyonId, cancellationToken);
        if (odenenTutar > 0)
        {
            throw new BaseException("Odeme alinmis rezervasyon dogrudan iptal edilemez. Once iade veya mahsup islemi yapilmalidir.", 400);
        }

        var statusBeforeCancellation = reservation.RezervasyonDurumu;
        reservation.RezervasyonDurumu = RezervasyonDurumlari.Iptal;
        await SetKonaklamaHakDurumuAsync(reservation.Id, RezervasyonKonaklamaHakDurumlari.Bekliyor, RezervasyonKonaklamaHakDurumlari.Iptal, cancellationToken);
        AppendHistoryEntry(
            reservation.Id,
            RezervasyonGecmisIslemTipleri.IptalEdildi,
            "Rezervasyon iptal edildi.",
            new { RezervasyonDurumu = statusBeforeCancellation },
            new { RezervasyonDurumu = reservation.RezervasyonDurumu });
        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return ToSaveResult(reservation);
    }

    private async Task SetKonaklamaHakDurumuAsync(int rezervasyonId, string fromDurum, string toDurum, CancellationToken cancellationToken)
    {
        var haklar = await _stysDbContext.RezervasyonKonaklamaHaklari
            .Where(x => x.RezervasyonId == rezervasyonId
                && x.AktifMi
                && !x.IsDeleted
                && x.Durum == fromDurum)
            .ToListAsync(cancellationToken);

        foreach (var hak in haklar)
        {
            hak.Durum = toDurum;
        }
    }

    private async Task EnsureCanRevertCancellationAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var ownAssignments = await (
            from atama in _stysDbContext.RezervasyonSegmentOdaAtamalari
            join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
            where segment.RezervasyonId == rezervasyonId
            select new
            {
                atama.OdaId,
                atama.AyrilanKisiSayisi,
                atama.KapasiteSnapshot,
                atama.PaylasimliMiSnapshot,
                segment.BaslangicTarihi,
                segment.BitisTarihi
            })
            .ToListAsync(cancellationToken);

        if (ownAssignments.Count == 0)
        {
            throw new BaseException("Iptal geri alinamadi. Rezervasyon segment/oda bilgisi bulunamadi.", 400);
        }

        var roomIds = ownAssignments
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        var minStart = ownAssignments.Min(x => x.BaslangicTarihi);
        var maxEnd = ownAssignments.Max(x => x.BitisTarihi);

        var activeBlocks = await _stysDbContext.OdaKullanimBloklari
            .Where(x =>
                x.AktifMi
                && roomIds.Contains(x.OdaId)
                && x.BaslangicTarihi < maxEnd
                && x.BitisTarihi > minStart)
            .Select(x => new
            {
                x.OdaId,
                x.BaslangicTarihi,
                x.BitisTarihi
            })
            .ToListAsync(cancellationToken);

        foreach (var own in ownAssignments)
        {
            if (activeBlocks.Any(x =>
                    x.OdaId == own.OdaId
                    && x.BaslangicTarihi < own.BitisTarihi
                    && x.BitisTarihi > own.BaslangicTarihi))
            {
                throw new BaseException("Iptal geri alinamadi. En az bir segmentte oda bakim/ariza nedeniyle musait degil.", 400);
            }

            var occupied = (await GetCurrentOccupancyByRoomAsync(
                    [own.OdaId],
                    own.BaslangicTarihi,
                    own.BitisTarihi,
                    cancellationToken,
                    rezervasyonId))
                .GetValueOrDefault(own.OdaId);

            if (!own.PaylasimliMiSnapshot)
            {
                if (occupied > 0)
                {
                    throw new BaseException("Iptal geri alinamadi. En az bir segmentte oda musait degil.", 400);
                }

                continue;
            }

            var kalanKapasite = own.KapasiteSnapshot - occupied;
            if (kalanKapasite < own.AyrilanKisiSayisi)
            {
                throw new BaseException("Iptal geri alinamadi. En az bir segmentte oda musait degil.", 400);
            }
        }
    }

    public async Task<RezervasyonOdemeOzetDto> GetOdemeOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        var ekHizmetler = await GetEkHizmetlerAsync(rezervasyonId, cancellationToken);
        var odemeler = await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == reservation.Id)
            .OrderByDescending(x => x.OdemeTarihi)
            .ThenByDescending(x => x.Id)
            .Select(x => new RezervasyonOdemeDto
            {
                Id = x.Id,
                OdemeTarihi = x.OdemeTarihi,
                OdemeTutari = x.OdemeTutari,
                ParaBirimi = x.ParaBirimi,
                OdemeTipi = x.OdemeTipi,
                Aciklama = x.Aciklama
            })
            .ToListAsync(cancellationToken);

        var ekHizmetToplami = ekHizmetler.Sum(x => x.ToplamTutar);
        var toplamUcret = reservation.ToplamUcret + ekHizmetToplami;
        var odenenTutar = odemeler.Sum(x => x.OdemeTutari);
        var kalanTutar = Math.Max(0m, toplamUcret - odenenTutar);

        return new RezervasyonOdemeOzetDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            KonaklamaUcreti = reservation.ToplamUcret,
            EkHizmetToplami = ekHizmetToplami,
            ToplamUcret = toplamUcret,
            OdenenTutar = odenenTutar,
            KalanTutar = kalanTutar,
            ParaBirimi = reservation.ParaBirimi,
            EkHizmetler = ekHizmetler,
            Odemeler = odemeler
        };
    }

    public async Task<RezervasyonEkHizmetSecenekleriDto> GetEkHizmetSecenekleriAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        var misafirler = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == reservation.Id && x.KatilimDurumu == KonaklayanKatilimDurumlari.Geldi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonEkHizmetMisafirSecenekDto
            {
                RezervasyonKonaklayanId = x.Id,
                SiraNo = x.SiraNo,
                AdSoyad = x.AdSoyad
            })
            .ToListAsync(cancellationToken);

        var stayStart = reservation.GirisTarihi.Date;
        var stayEnd = reservation.CikisTarihi.Date;
        var paketCakismaPolitikasi = await _stysDbContext.Tesisler
            .Where(x => x.Id == reservation.TesisId)
            .Select(x => x.EkHizmetPaketCakismaPolitikasi)
            .SingleOrDefaultAsync(cancellationToken) ?? EkHizmetPaketCakismaPolitikalari.OnayIste;

        var tarifeler = await _stysDbContext.EkHizmetTarifeleri
            .Include(x => x.EkHizmet)
            .Where(x =>
                x.TesisId == reservation.TesisId
                && x.AktifMi
                && x.EkHizmet != null
                && x.EkHizmet.AktifMi
                && x.BaslangicTarihi <= stayEnd
                && x.BitisTarihi >= stayStart)
            .OrderBy(x => x.EkHizmet!.Ad)
            .ThenBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonEkHizmetTarifeSecenekDto
            {
                Id = x.Id,
                EkHizmetId = x.EkHizmetId,
                Ad = x.EkHizmet != null ? x.EkHizmet.Ad : string.Empty,
                Aciklama = x.EkHizmet != null ? x.EkHizmet.Aciklama : null,
                BirimAdi = x.EkHizmet != null ? x.EkHizmet.BirimAdi : string.Empty,
                PaketIcerikHizmetKodu = x.EkHizmet != null ? x.EkHizmet.PaketIcerikHizmetKodu : null,
                BirimFiyat = x.BirimFiyat,
                ParaBirimi = x.ParaBirimi,
                BaslangicTarihi = x.BaslangicTarihi,
                BitisTarihi = x.BitisTarihi
            })
            .ToListAsync(cancellationToken);

        var paketIcerikKodlari = reservation.KonaklamaTipiId.HasValue
            ? (await GetEffectiveKonaklamaTipiIcerikMapAsync(
                    reservation.TesisId,
                    [reservation.KonaklamaTipiId.Value],
                    cancellationToken))
                .GetValueOrDefault(reservation.KonaklamaTipiId.Value, [])
                .Select(x => x.HizmetKodu)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        ApplyEkHizmetPaketIcerigiWarnings(tarifeler, paketIcerikKodlari);

        return new RezervasyonEkHizmetSecenekleriDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            PaketCakismaPolitikasi = paketCakismaPolitikasi,
            Misafirler = misafirler,
            Tarifeler = tarifeler
        };
    }

    private static void ApplyEkHizmetPaketIcerigiWarnings(
        List<RezervasyonEkHizmetTarifeSecenekDto> tarifeler,
        IReadOnlyCollection<string> paketIcerikKodlari)
    {
        if (tarifeler.Count == 0 || paketIcerikKodlari.Count == 0)
        {
            return;
        }

        var normalizedKodlar = paketIcerikKodlari
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tarife in tarifeler)
        {
            if (string.IsNullOrWhiteSpace(tarife.PaketIcerikHizmetKodu) || !normalizedKodlar.Contains(tarife.PaketIcerikHizmetKodu))
            {
                continue;
            }

            tarife.PaketIcerigiUyariMesaji =
                $"Secili konaklama tipi bu hizmeti paket icinde zaten sunuyor: {KonaklamaTipiIcerikHizmetKodlari.GetAd(tarife.PaketIcerikHizmetKodu)}. Gerekliyse ek satisa devam edebilirsiniz.";
        }
    }

    public async Task<RezervasyonOdemeOzetDto> KaydetEkHizmetAsync(int rezervasyonId, RezervasyonEkHizmetKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await EnsureReservationAllowsExtraServiceCreateAsync(reservation);
        var context = await ResolveEkHizmetContextAsync(reservation, request, cancellationToken);

        var birimFiyat = ResolveRezervasyonEkHizmetBirimFiyati(request.BirimFiyat, context.Tarife.BirimFiyat);
        var toplamTutar = Math.Round(birimFiyat * request.Miktar, 2, MidpointRounding.AwayFromZero);
        var aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();

        await _stysDbContext.RezervasyonEkHizmetler.AddAsync(new RezervasyonEkHizmet
        {
            RezervasyonId = reservation.Id,
            RezervasyonKonaklayanId = context.Konaklayan.Id,
            EkHizmetId = context.Tarife.EkHizmetId,
            EkHizmetTarifeId = context.Tarife.Id,
            RezervasyonSegmentId = context.Segment.Id,
            OdaId = context.KonaklayanAtama.OdaId,
            YatakNoSnapshot = context.KonaklayanAtama.YatakNo,
            TarifeAdiSnapshot = context.Tarife.EkHizmet!.Ad,
            BirimAdiSnapshot = context.Tarife.EkHizmet.BirimAdi,
            OdaNoSnapshot = context.OdaAtama.OdaNoSnapshot,
            BinaAdiSnapshot = context.OdaAtama.BinaAdiSnapshot,
            HizmetTarihi = request.HizmetTarihi,
            Miktar = request.Miktar,
            BirimFiyat = birimFiyat,
            ToplamTutar = toplamTutar,
            ParaBirimi = reservation.ParaBirimi,
            Aciklama = aciklama
        }, cancellationToken);

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.EkHizmetEklendi,
            "Rezervasyona ek hizmet eklendi.",
            null,
            new
            {
                Konaklayan = context.Konaklayan.AdSoyad,
                Hizmet = context.Tarife.EkHizmet!.Ad,
                request.Miktar,
                context.Tarife.EkHizmet.BirimAdi,
                BirimFiyat = birimFiyat,
                ToplamTutar = toplamTutar,
                HizmetTarihi = request.HizmetTarihi,
                OdaNo = context.OdaAtama.OdaNoSnapshot,
                BinaAdi = context.OdaAtama.BinaAdiSnapshot,
                YatakNo = context.KonaklayanAtama.YatakNo,
                Aciklama = aciklama
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetOdemeOzetiAsync(rezervasyonId, cancellationToken);
    }

    public async Task<RezervasyonOdemeOzetDto> GuncelleEkHizmetAsync(int rezervasyonId, int ekHizmetId, RezervasyonEkHizmetKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (ekHizmetId <= 0)
        {
            throw new BaseException("Gecersiz ek hizmet id.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await EnsureReservationAllowsExtraServiceEditAsync(reservation, cancellationToken);

        var ekHizmet = await _stysDbContext.RezervasyonEkHizmetler
            .Include(x => x.RezervasyonKonaklayan)
            .FirstOrDefaultAsync(x => x.Id == ekHizmetId && x.RezervasyonId == rezervasyonId, cancellationToken);

        if (ekHizmet is null)
        {
            throw new BaseException("Ek hizmet kaydi bulunamadi.", 404);
        }

        var oncekiDeger = new
        {
            Konaklayan = ekHizmet.RezervasyonKonaklayan?.AdSoyad,
            Hizmet = ekHizmet.TarifeAdiSnapshot,
            ekHizmet.Miktar,
            BirimAdi = ekHizmet.BirimAdiSnapshot,
            ekHizmet.BirimFiyat,
            ekHizmet.ToplamTutar,
            ekHizmet.HizmetTarihi,
            OdaNo = ekHizmet.OdaNoSnapshot,
            BinaAdi = ekHizmet.BinaAdiSnapshot,
            YatakNo = ekHizmet.YatakNoSnapshot,
            ekHizmet.Aciklama
        };

        var context = await ResolveEkHizmetContextAsync(reservation, request, cancellationToken);
        var birimFiyat = ResolveRezervasyonEkHizmetBirimFiyati(request.BirimFiyat, context.Tarife.BirimFiyat);
        var toplamTutar = Math.Round(birimFiyat * request.Miktar, 2, MidpointRounding.AwayFromZero);
        var aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();
        await EnsureExtraServiceAdjustmentKeepsBalanceNonNegativeAsync(rezervasyonId, ekHizmet.ToplamTutar, toplamTutar, cancellationToken);

        ekHizmet.RezervasyonKonaklayanId = context.Konaklayan.Id;
        ekHizmet.EkHizmetId = context.Tarife.EkHizmetId;
        ekHizmet.EkHizmetTarifeId = context.Tarife.Id;
        ekHizmet.RezervasyonSegmentId = context.Segment.Id;
        ekHizmet.OdaId = context.KonaklayanAtama.OdaId;
        ekHizmet.YatakNoSnapshot = context.KonaklayanAtama.YatakNo;
        ekHizmet.TarifeAdiSnapshot = context.Tarife.EkHizmet!.Ad;
        ekHizmet.BirimAdiSnapshot = context.Tarife.EkHizmet.BirimAdi;
        ekHizmet.OdaNoSnapshot = context.OdaAtama.OdaNoSnapshot;
        ekHizmet.BinaAdiSnapshot = context.OdaAtama.BinaAdiSnapshot;
        ekHizmet.HizmetTarihi = request.HizmetTarihi;
        ekHizmet.Miktar = request.Miktar;
        ekHizmet.BirimFiyat = birimFiyat;
        ekHizmet.ToplamTutar = toplamTutar;
        ekHizmet.Aciklama = aciklama;

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.EkHizmetGuncellendi,
            "Rezervasyondaki ek hizmet kaydi guncellendi.",
            oncekiDeger,
            new
            {
                Konaklayan = context.Konaklayan.AdSoyad,
                Hizmet = context.Tarife.EkHizmet!.Ad,
                request.Miktar,
                context.Tarife.EkHizmet.BirimAdi,
                BirimFiyat = birimFiyat,
                ToplamTutar = toplamTutar,
                HizmetTarihi = request.HizmetTarihi,
                OdaNo = context.OdaAtama.OdaNoSnapshot,
                BinaAdi = context.OdaAtama.BinaAdiSnapshot,
                YatakNo = context.KonaklayanAtama.YatakNo,
                Aciklama = aciklama
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetOdemeOzetiAsync(rezervasyonId, cancellationToken);
    }

    public async Task<RezervasyonOdemeOzetDto> SilEkHizmetAsync(int rezervasyonId, int ekHizmetId, CancellationToken cancellationToken = default)
    {
        if (ekHizmetId <= 0)
        {
            throw new BaseException("Gecersiz ek hizmet id.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await EnsureReservationAllowsExtraServiceEditAsync(reservation, cancellationToken);

        var ekHizmet = await _stysDbContext.RezervasyonEkHizmetler
            .Include(x => x.RezervasyonKonaklayan)
            .FirstOrDefaultAsync(x => x.Id == ekHizmetId && x.RezervasyonId == rezervasyonId, cancellationToken);

        if (ekHizmet is null)
        {
            throw new BaseException("Ek hizmet kaydi bulunamadi.", 404);
        }

        await EnsureExtraServiceDeletionAllowedAsync(rezervasyonId, ekHizmet.ToplamTutar, cancellationToken);

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.EkHizmetSilindi,
            "Rezervasyondaki ek hizmet kaydi silindi.",
            new
            {
                Konaklayan = ekHizmet.RezervasyonKonaklayan?.AdSoyad,
                Hizmet = ekHizmet.TarifeAdiSnapshot,
                ekHizmet.Miktar,
                BirimAdi = ekHizmet.BirimAdiSnapshot,
                ekHizmet.BirimFiyat,
                ekHizmet.ToplamTutar,
                ekHizmet.HizmetTarihi,
                OdaNo = ekHizmet.OdaNoSnapshot,
                BinaAdi = ekHizmet.BinaAdiSnapshot,
                YatakNo = ekHizmet.YatakNoSnapshot,
                ekHizmet.Aciklama
            },
            null);

        _stysDbContext.RezervasyonEkHizmetler.Remove(ekHizmet);
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetOdemeOzetiAsync(rezervasyonId, cancellationToken);
    }

    public async Task<RezervasyonOdemeOzetDto> KaydetOdemeAsync(int rezervasyonId, RezervasyonOdemeKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.OdemeTutari <= 0)
        {
            throw new BaseException("Odeme tutari sifirdan buyuk olmalidir.", 400);
        }

        var normalizedOdemeTipi = NormalizeOdemeTipi(request.OdemeTipi);
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await EnsureReservationAllowsPaymentCreateAsync(reservation);

        var odenenTutar = await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == reservation.Id)
            .Select(x => (decimal?)x.OdemeTutari)
            .SumAsync(cancellationToken) ?? 0m;

        var ekHizmetToplami = await GetRezervasyonEkHizmetToplamiAsync(rezervasyonId, cancellationToken);
        var toplamUcret = reservation.ToplamUcret + ekHizmetToplami;
        var kalanTutar = toplamUcret - odenenTutar;
        if (kalanTutar <= 0)
        {
            throw new BaseException("Rezervasyonun odeme bakiyesi bulunmuyor.", 400);
        }

        if (request.OdemeTutari > kalanTutar)
        {
            throw new BaseException("Odeme tutari kalan bakiyeden buyuk olamaz.", 400);
        }

        var yeniOdenenTutar = odenenTutar + request.OdemeTutari;
        var yeniKalanTutar = Math.Max(0m, toplamUcret - yeniOdenenTutar);

        await _stysDbContext.RezervasyonOdemeler.AddAsync(new RezervasyonOdeme
        {
            RezervasyonId = reservation.Id,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = request.OdemeTutari,
            ParaBirimi = reservation.ParaBirimi,
            OdemeTipi = normalizedOdemeTipi,
            Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim()
        }, cancellationToken);

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.OdemeKaydedildi,
            "Rezervasyona odeme eklendi.",
            new
            {
                OdenenTutar = odenenTutar,
                KalanTutar = kalanTutar
            },
            new
            {
                OdemeTutari = request.OdemeTutari,
                OdemeTipi = normalizedOdemeTipi,
                Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim(),
                OdenenTutar = yeniOdenenTutar,
                KalanTutar = yeniKalanTutar
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            reservation.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "Odeme",
                Baslik = "Rezervasyon Odemesi Alindi",
                Mesaj = $"{reservation.ReferansNo} rezervasyonu icin {request.OdemeTutari:N2} {reservation.ParaBirimi} odeme kaydedildi.",
                Severity = BildirimSeverityleri.Info,
                Link = "/rezervasyon-yonetimi"
            },
            cancellationToken);

        return await GetOdemeOzetiAsync(rezervasyonId, cancellationToken);
    }

    public async Task<RezervasyonDetayDto> GuncelleKonaklamaHakkiDurumuAsync(
        int rezervasyonId,
        int hakId,
        RezervasyonKonaklamaHakkiDurumGuncelleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (hakId <= 0)
        {
            throw new BaseException("Gecersiz konaklama hakki id.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyonda konaklama hakki guncellenemez.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Konaklama hakki durumu yalnizca check-in tamamlanan rezervasyonda guncellenebilir.", 400);
        }

        var hedefDurum = (request.Durum ?? string.Empty).Trim();
        if (hedefDurum != RezervasyonKonaklamaHakDurumlari.Bekliyor
            && hedefDurum != RezervasyonKonaklamaHakDurumlari.Kullanildi)
        {
            throw new BaseException("Konaklama hakki icin gecersiz durum secildi.", 400);
        }

        var hak = await _stysDbContext.RezervasyonKonaklamaHaklari
            .FirstOrDefaultAsync(x => x.Id == hakId
                && x.RezervasyonId == rezervasyonId
                && x.AktifMi
                && !x.IsDeleted, cancellationToken);

        if (hak is null)
        {
            throw new BaseException("Konaklama hakki bulunamadi.", 404);
        }

        var oncekiDurum = hak.Durum;
        if (oncekiDurum == RezervasyonKonaklamaHakDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen konaklama hakki manuel olarak guncellenemez.", 400);
        }

        if (oncekiDurum == hedefDurum)
        {
            return await GetRezervasyonDetayAsync(rezervasyonId, cancellationToken)
                ?? throw new BaseException("Rezervasyon detayi bulunamadi.", 404);
        }

        var tuketimKaydiVar = await _stysDbContext.RezervasyonKonaklamaHakkiTuketimKayitlari
            .AnyAsync(x => x.RezervasyonKonaklamaHakkiId == hak.Id && x.AktifMi && !x.IsDeleted, cancellationToken);

        if (tuketimKaydiVar)
        {
            throw new BaseException("Tuketim kaydi bulunan konaklama hakki manuel olarak guncellenemez.", 400);
        }

        var gecisGecerli =
            (oncekiDurum == RezervasyonKonaklamaHakDurumlari.Bekliyor && hedefDurum == RezervasyonKonaklamaHakDurumlari.Kullanildi)
            || (oncekiDurum == RezervasyonKonaklamaHakDurumlari.Kullanildi && hedefDurum == RezervasyonKonaklamaHakDurumlari.Bekliyor);

        if (!gecisGecerli)
        {
            throw new BaseException("Konaklama hakki icin izin verilmeyen durum gecisi.", 400);
        }

        hak.Durum = hedefDurum;

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.KonaklamaHakkiDurumuGuncellendi,
            "Konaklama hakki durumu guncellendi.",
            new
            {
                HakId = hak.Id,
                hak.HizmetKodu,
                HizmetAdi = hak.HizmetAdiSnapshot,
                oncekiDurum
            },
            new
            {
                HakId = hak.Id,
                hak.HizmetKodu,
                HizmetAdi = hak.HizmetAdiSnapshot,
                Durum = hak.Durum
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetRezervasyonDetayAsync(rezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon detayi bulunamadi.", 404);
    }

    public async Task<RezervasyonDetayDto> KaydetKonaklamaHakkiTuketimAsync(
        int rezervasyonId,
        int hakId,
        RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (hakId <= 0)
        {
            throw new BaseException("Gecersiz konaklama hakki id.", 400);
        }

        if (request.Miktar <= 0)
        {
            throw new BaseException("Tuketim miktari sifirdan buyuk olmalidir.", 400);
        }

        if (request.TuketimTarihi == default)
        {
            throw new BaseException("Tuketim tarihi zorunludur.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        ValidateReservationAllowsHakTuketim(reservation);

        var hak = await _stysDbContext.RezervasyonKonaklamaHaklari
            .FirstOrDefaultAsync(x => x.Id == hakId
                && x.RezervasyonId == rezervasyonId
                && x.AktifMi
                && !x.IsDeleted, cancellationToken);

        if (hak is null)
        {
            throw new BaseException("Konaklama hakki bulunamadi.", 404);
        }

        if (hak.Durum == RezervasyonKonaklamaHakDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen konaklama hakki icin tuketim kaydi olusturulamaz.", 400);
        }

        ValidateHakTuketimRequest(reservation, hak, request.TuketimTarihi, request.Miktar);

        var mevcutTuketim = await GetToplamHakTuketimiAsync(hak.Id, cancellationToken);
        if (hak.KullanimTipi == KonaklamaTipiIcerikKullanimTipleri.Adetli
            && mevcutTuketim + request.Miktar > hak.Miktar)
        {
            throw new BaseException($"Bu hak icin en fazla {hak.Miktar} kullanim kaydi girilebilir.", 400);
        }

        var tuketimNoktasi = await ResolveTuketimNoktasiAsync(reservation.TesisId, hak.KullanimNoktasi, request.IsletmeAlaniId, cancellationToken);

        var kayit = new RezervasyonKonaklamaHakkiTuketimKaydi
        {
            RezervasyonId = rezervasyonId,
            RezervasyonKonaklamaHakkiId = hak.Id,
            IsletmeAlaniId = tuketimNoktasi?.Id,
            TuketimTarihi = request.TuketimTarihi,
            Miktar = request.Miktar,
            KullanimTipi = hak.KullanimTipi,
            KullanimNoktasi = hak.KullanimNoktasi,
            KullanimNoktasiAdiSnapshot = hak.KullanimNoktasiAdiSnapshot,
            TuketimNoktasiAdi = NormalizeOptionalText(tuketimNoktasi?.Ad, 128),
            Aciklama = NormalizeOptionalText(request.Aciklama, 256),
            AktifMi = true
        };

        _stysDbContext.RezervasyonKonaklamaHakkiTuketimKayitlari.Add(kayit);

        var oncekiDurum = hak.Durum;
        var yeniTuketim = mevcutTuketim + request.Miktar;
        hak.Durum = CalculateHakDurumu(hak, yeniTuketim);

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.KonaklamaHakkiTuketimiKaydedildi,
            "Konaklama hakki icin tuketim kaydi eklendi.",
            null,
            new
            {
                HakId = hak.Id,
                hak.HizmetKodu,
                HizmetAdi = hak.HizmetAdiSnapshot,
                TuketimTarihi = kayit.TuketimTarihi,
                kayit.Miktar,
                kayit.KullanimNoktasi,
                kayit.TuketimNoktasiAdi,
                OncekiDurum = oncekiDurum,
                YeniDurum = hak.Durum
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetRezervasyonDetayAsync(rezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon detayi bulunamadi.", 404);
    }

    public async Task<RezervasyonDetayDto> SilKonaklamaHakkiTuketimAsync(
        int rezervasyonId,
        int hakId,
        int tuketimKaydiId,
        CancellationToken cancellationToken = default)
    {
        if (hakId <= 0 || tuketimKaydiId <= 0)
        {
            throw new BaseException("Gecersiz konaklama hakki tuketim kaydi.", 400);
        }

        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        ValidateReservationAllowsHakTuketim(reservation);

        var hak = await _stysDbContext.RezervasyonKonaklamaHaklari
            .FirstOrDefaultAsync(x => x.Id == hakId
                && x.RezervasyonId == rezervasyonId
                && x.AktifMi
                && !x.IsDeleted, cancellationToken);

        if (hak is null)
        {
            throw new BaseException("Konaklama hakki bulunamadi.", 404);
        }

        var kayit = await _stysDbContext.RezervasyonKonaklamaHakkiTuketimKayitlari
            .FirstOrDefaultAsync(x => x.Id == tuketimKaydiId
                && x.RezervasyonId == rezervasyonId
                && x.RezervasyonKonaklamaHakkiId == hakId
                && x.AktifMi
                && !x.IsDeleted, cancellationToken);

        if (kayit is null)
        {
            throw new BaseException("Konaklama hakki tuketim kaydi bulunamadi.", 404);
        }

        kayit.AktifMi = false;
        kayit.IsDeleted = true;
        kayit.DeletedAt = DateTime.UtcNow;
        kayit.DeletedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";

        var oncekiDurum = hak.Durum;
        var guncelTuketim = await GetToplamHakTuketimiAsync(hak.Id, cancellationToken) - kayit.Miktar;
        hak.Durum = CalculateHakDurumu(hak, Math.Max(0, guncelTuketim));

        AppendHistoryEntry(
            rezervasyonId,
            RezervasyonGecmisIslemTipleri.KonaklamaHakkiTuketimiSilindi,
            "Konaklama hakki tuketim kaydi silindi.",
            new
            {
                HakId = hak.Id,
                hak.HizmetKodu,
                HizmetAdi = hak.HizmetAdiSnapshot,
                TuketimKaydiId = kayit.Id,
                kayit.TuketimTarihi,
                kayit.Miktar,
                kayit.KullanimNoktasi,
                kayit.TuketimNoktasiAdi,
                OncekiDurum = oncekiDurum
            },
            new
            {
                HakId = hak.Id,
                Durum = hak.Durum
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return await GetRezervasyonDetayAsync(rezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon detayi bulunamadi.", 404);
    }

    public async Task<OdemeRaporDto> GetOdemeRaporuAsync(
        IReadOnlyCollection<int> tesisIds,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken = default)
    {
        var normalizedTesisIds = tesisIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (normalizedTesisIds.Count == 0)
        {
            throw new BaseException("En az bir tesis secimi zorunludur.", 400);
        }

        var rangeStart = baslangicTarihi.Date;
        var rangeEndExclusive = bitisTarihi.Date.AddDays(1);
        if (rangeStart >= rangeEndExclusive)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
        }

        foreach (var tesisId in normalizedTesisIds)
        {
            await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        }

        var rawRows = await _stysDbContext.RezervasyonOdemeler
            .Where(x =>
                x.Rezervasyon != null
                && x.Rezervasyon.AktifMi
                && normalizedTesisIds.Contains(x.Rezervasyon.TesisId)
                && x.OdemeTarihi >= rangeStart
                && x.OdemeTarihi < rangeEndExclusive)
            .Select(x => new
            {
                x.RezervasyonId,
                x.OdemeTutari,
                x.CreatedBy,
                RezervasyonNo = x.Rezervasyon!.ReferansNo,
                x.Rezervasyon.ToplamBazUcret,
                x.Rezervasyon.ToplamUcret,
                TesisId = x.Rezervasyon.TesisId,
                TesisAdi = x.Rezervasyon.Tesis!.Ad
            })
            .ToListAsync(cancellationToken);

        var reportRows = rawRows
            .GroupBy(x => new
            {
                x.RezervasyonId,
                x.RezervasyonNo,
                x.ToplamBazUcret,
                x.ToplamUcret,
                x.TesisId,
                x.TesisAdi
            })
            .Select(group => new OdemeRaporSatirDto
            {
                TesisId = group.Key.TesisId,
                TesisAdi = group.Key.TesisAdi,
                RezervasyonNo = group.Key.RezervasyonNo,
                OdemeYapan = string.Join(", ", group
                    .Select(x => x.CreatedBy)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .DefaultIfEmpty("-")),
                ToplamBazUcret = group.Key.ToplamBazUcret,
                ToplamIndirim = Math.Max(0m, group.Key.ToplamBazUcret - group.Key.ToplamUcret),
                ToplamOdeme = group.Sum(x => x.OdemeTutari)
            })
            .OrderBy(x => x.TesisAdi)
            .ThenBy(x => x.RezervasyonNo)
            .ToList();

        return new OdemeRaporDto
        {
            TesisIds = normalizedTesisIds,
            BaslangicTarihi = rangeStart,
            BitisTarihi = bitisTarihi.Date,
            Satirlar = reportRows,
            ToplamGelir = reportRows.Sum(x => x.ToplamOdeme)
        };
    }

    private async Task<SenaryoFiyatHesaplamaSonucuDto> CalculateScenarioPriceAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        int kisiSayisi,
        bool tekKisilikFiyatUygulansinMi,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        IReadOnlyCollection<SenaryoFiyatHesaplaSegmentDto> segmentler,
        IReadOnlyCollection<int> seciliIndirimKuraliIds,
        CancellationToken cancellationToken)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        await EnsureTesisHasMisafirTipiAsync(tesisId, misafirTipiId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(tesisId, konaklamaTipiId, cancellationToken);
        ValidatePricingRequest(tesisId, misafirTipiId, konaklamaTipiId, kisiSayisi, tekKisilikFiyatUygulansinMi, baslangicTarihi, bitisTarihi, segmentler);
        await EnsureSeasonRuleComplianceAsync(tesisId, baslangicTarihi, bitisTarihi, cancellationToken);

        var roomIds = segmentler.SelectMany(x => x.OdaAtamalari).Select(x => x.OdaId).Distinct().ToList();
        var roomMaps = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where roomIds.Contains(oda.Id)
                  && oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
            select new
            {
                OdaId = oda.Id,
                bina.TesisId,
                OdaTipiId = odaTipi.Id,
                odaTipi.PaylasimliMi
            })
            .ToListAsync(cancellationToken);

        if (roomMaps.Count != roomIds.Count || roomMaps.Any(x => x.TesisId != tesisId))
        {
            throw new BaseException("Senaryodaki odalardan en az biri gecersiz veya tesis kapsaminda degil.", 400);
        }

        var roomTypeIds = roomMaps.Select(x => x.OdaTipiId).Distinct().ToList();
        var roomTypeByRoomId = roomMaps.ToDictionary(x => x.OdaId, x => new RoomTypePricingInfo(x.OdaTipiId, x.PaylasimliMi));
        var tesisSaatleri = await _stysDbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .Select(x => new { x.GirisSaati, x.CikisSaati })
            .FirstOrDefaultAsync(cancellationToken);

        if (tesisSaatleri is null)
        {
            throw new BaseException("Tesis bulunamadi.", 404);
        }

        var minDate = segmentler.Min(x => x.BaslangicTarihi).Date;
        var maxDate = segmentler.Max(x => x.BitisTarihi).Date;
        var fiyatKayitlari = await _stysDbContext.OdaFiyatlari
            .Where(x =>
                roomTypeIds.Contains(x.TesisOdaTipiId)
                && x.KonaklamaTipiId == konaklamaTipiId
                && x.MisafirTipiId == misafirTipiId
                && x.KisiSayisi == 1
                && x.AktifMi
                && x.BaslangicTarihi <= maxDate
                && x.BitisTarihi >= minDate)
            .OrderByDescending(x => x.BaslangicTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var currency = string.Empty;
        var baseTotal = 0m;

        foreach (var chargeWindow in EnumerateChargeWindows(
                     baslangicTarihi,
                     bitisTarihi,
                     tesisSaatleri.GirisSaati,
                     tesisSaatleri.CikisSaati))
        {
            var aktifSegment = segmentler
                .Where(x => x.BaslangicTarihi <= chargeWindow.WindowStart && x.BitisTarihi > chargeWindow.WindowStart)
                .OrderByDescending(x => x.BaslangicTarihi)
                .FirstOrDefault();

            if (aktifSegment is null)
            {
                throw new BaseException($"Senaryo icin {chargeWindow.ChargeDay:yyyy-MM-dd} tarihinde aktif segment bulunamadi.", 400);
            }

            foreach (var atama in aktifSegment.OdaAtamalari)
            {
                var roomTypeInfo = roomTypeByRoomId[atama.OdaId];
                var applicableRows = fiyatKayitlari
                    .Where(x =>
                        x.TesisOdaTipiId == roomTypeInfo.OdaTipiId
                        && x.BaslangicTarihi.Date <= chargeWindow.ChargeDay
                        && x.BitisTarihi.Date >= chargeWindow.ChargeDay)
                    .ToList();

                if (applicableRows.Count == 0)
                {
                    throw new BaseException($"Senaryo icin {chargeWindow.ChargeDay:yyyy-MM-dd} tarihinde uygun oda fiyati bulunamadi.", 400);
                }

                var fiyat = ResolveScenarioPrice(
                    applicableRows,
                    roomTypeInfo.PaylasimliMi,
                    atama.AyrilanKisiSayisi,
                    kisiSayisi,
                    tekKisilikFiyatUygulansinMi,
                    chargeWindow.ChargeDay);

                if (string.IsNullOrWhiteSpace(currency))
                {
                    currency = fiyat.ParaBirimi;
                }
                else if (!currency.Equals(fiyat.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BaseException("Senaryo fiyatlari birden fazla para birimi iceriyor.", 400);
                }

                baseTotal += fiyat.Tutar;
            }
        }

        var finalTotal = baseTotal;
        var appliedDiscounts = new List<UygulananIndirimDto>();
        if (seciliIndirimKuraliIds.Count > 0)
        {
            var selectedSet = seciliIndirimKuraliIds
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();

            if (selectedSet.Count > 0)
            {
                var candidateRules = await QueryApplicableDiscountRulesAsync(
                    tesisId,
                    misafirTipiId,
                    konaklamaTipiId,
                    baslangicTarihi,
                    bitisTarihi,
                    cancellationToken);

                var selectedRules = candidateRules
                    .Where(x => selectedSet.Contains(x.Id))
                    .ToList();

                foreach (var rule in selectedRules)
                {
                    var discountAmount = CalculateDiscountAmount(rule, finalTotal);
                    if (discountAmount <= 0)
                    {
                        continue;
                    }

                    finalTotal -= discountAmount;
                    appliedDiscounts.Add(new UygulananIndirimDto
                    {
                        IndirimKuraliId = rule.Id,
                        KuralAdi = rule.Ad,
                        IndirimTutari = discountAmount,
                        SonrasiTutar = finalTotal
                    });

                    if (!rule.BirlesebilirMi)
                    {
                        break;
                    }
                }
            }
        }

        return new SenaryoFiyatHesaplamaSonucuDto
        {
            ToplamBazUcret = baseTotal,
            ToplamNihaiUcret = finalTotal,
            ParaBirimi = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.ToUpperInvariant(),
            UygulananIndirimler = appliedDiscounts
        };
    }

    private async Task<List<IndirimKurali>> QueryApplicableDiscountRulesAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var rules = await _stysDbContext.IndirimKurallari
            .Where(x =>
                x.AktifMi
                && x.BaslangicTarihi <= bitisTarihi
                && x.BitisTarihi >= baslangicTarihi
                && (x.KapsamTipi == IndirimKapsamTipleri.Sistem
                    || (x.KapsamTipi == IndirimKapsamTipleri.Tesis && x.TesisId == tesisId)))
            .Include(x => x.MisafirTipiKisitlari)
            .Include(x => x.KonaklamaTipiKisitlari)
            .OrderBy(x => x.KapsamTipi == IndirimKapsamTipleri.Tesis ? 0 : 1)
            .ThenByDescending(x => x.Oncelik)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rules
            .Where(x => IsDiscountRuleApplicable(x, misafirTipiId, konaklamaTipiId))
            .ToList();
    }

    private static bool IsDiscountRuleApplicable(IndirimKurali rule, int misafirTipiId, int konaklamaTipiId)
    {
        if (rule.KonaklamaTipiKisitlari.Count > 0 && !rule.KonaklamaTipiKisitlari.Any(x => x.KonaklamaTipiId == konaklamaTipiId))
        {
            return false;
        }

        if (rule.MisafirTipiKisitlari.Count > 0 && !rule.MisafirTipiKisitlari.Any(x => x.MisafirTipiId == misafirTipiId))
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

        var discount = rule.IndirimTipi.Equals(IndirimTipleri.Yuzde, StringComparison.OrdinalIgnoreCase)
            ? Math.Round(currentAmount * rule.Deger / 100m, 2, MidpointRounding.AwayFromZero)
            : rule.Deger;

        return Math.Min(currentAmount, Math.Max(0, discount));
    }

    private async Task EnsureSeasonRuleComplianceAsync(
        int tesisId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var normalizedStart = baslangicTarihi.Date;
        var normalizedEnd = bitisTarihi.Date;

        var rules = await _stysDbContext.SezonKurallari
            .Where(x =>
                x.TesisId == tesisId
                && x.AktifMi
                && x.BaslangicTarihi <= normalizedEnd
                && x.BitisTarihi >= normalizedStart)
            .Select(x => new
            {
                x.StopSaleMi,
                x.MinimumGece
            })
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return;
        }

        if (rules.Any(x => x.StopSaleMi))
        {
            throw new BaseException("Secilen tarih araliginda stop-sale aktif oldugu icin islem yapilamaz.", 400);
        }

        var requiredMinimumNight = rules
            .Select(x => x.MinimumGece > 0 ? x.MinimumGece : 1)
            .DefaultIfEmpty(1)
            .Max();

        var tesisSaatleri = await _stysDbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .Select(x => new { x.GirisSaati, x.CikisSaati })
            .FirstOrDefaultAsync(cancellationToken);

        if (tesisSaatleri is null)
        {
            throw new BaseException("Tesis bulunamadi.", 404);
        }

        var geceSayisi = CalculateNightCount(
            baslangicTarihi,
            bitisTarihi,
            tesisSaatleri.GirisSaati,
            tesisSaatleri.CikisSaati);

        if (geceSayisi < requiredMinimumNight)
        {
            throw new BaseException($"Secilen tarih araligi icin minimum {requiredMinimumNight} gece konaklama zorunludur.", 400);
        }
    }

    private static int CalculateNightCount(
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        TimeSpan girisSaati,
        TimeSpan cikisSaati)
    {
        return EnumerateChargeWindows(baslangicTarihi, bitisTarihi, girisSaati, cikisSaati).Count();
    }

    private static IEnumerable<(DateTime ChargeDay, DateTime WindowStart)> EnumerateChargeWindows(
        DateTime baslangic,
        DateTime bitis,
        TimeSpan girisSaati,
        TimeSpan cikisSaati)
    {
        if (bitis <= baslangic)
        {
            yield break;
        }

        var startDate = baslangic.Date;
        var firstWindowStart = startDate.Add(girisSaati);
        var firstWindowEnd = startDate.AddDays(1).Add(cikisSaati);

        // Ilk gun: giris saati -> ertesi gun cikis saati
        if (bitis > firstWindowStart && baslangic < firstWindowEnd)
        {
            var effectiveStart = baslangic > firstWindowStart ? baslangic : firstWindowStart;
            yield return (startDate, effectiveStart);
        }

        // Sonraki gunler: cikis saati -> ertesi gun cikis saati
        for (var windowStart = firstWindowEnd; windowStart < bitis; windowStart = windowStart.AddDays(1))
        {
            var windowEnd = windowStart.AddDays(1);
            if (bitis > windowStart && baslangic < windowEnd)
            {
                var effectiveStart = baslangic > windowStart ? baslangic : windowStart;
                yield return (windowStart.Date, effectiveStart);
            }
        }
    }

    private static void ValidatePricingRequest(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        int kisiSayisi,
        bool tekKisilikFiyatUygulansinMi,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        IReadOnlyCollection<SenaryoFiyatHesaplaSegmentDto> segmentler)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (misafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (konaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (kisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        if (tekKisilikFiyatUygulansinMi && kisiSayisi != 1)
        {
            throw new BaseException("Tek kisilik fiyat yalnizca tek konaklayan icin secilebilir.", 400);
        }

        if (baslangicTarihi >= bitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        if (segmentler.Count == 0)
        {
            throw new BaseException("En az bir senaryo segmenti gereklidir.", 400);
        }

        foreach (var segment in segmentler)
        {
            if (segment.BaslangicTarihi >= segment.BitisTarihi)
            {
                throw new BaseException("Segment baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
            }

            if (segment.BaslangicTarihi < baslangicTarihi || segment.BitisTarihi > bitisTarihi)
            {
                throw new BaseException("Segment araligi rezervasyon araligi disina cikamaz.", 400);
            }

            if (segment.OdaAtamalari.Count == 0 || segment.OdaAtamalari.Any(x => x.OdaId <= 0 || x.AyrilanKisiSayisi <= 0))
            {
                throw new BaseException("Segment oda atamalari gecersiz.", 400);
            }
        }
    }

    private static void ValidateRequest(UygunOdaAramaRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        var baslangic = request.BaslangicTarihi;
        var bitis = request.BitisTarihi;
        if (baslangic >= bitis)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }
    }

    private static void ValidateScenarioRequest(KonaklamaSenaryoAramaRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.MisafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (request.KonaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        if (request.BaslangicTarihi >= request.BitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        if (request.KonaklayanCinsiyetleri.Count > 0 && request.KonaklayanCinsiyetleri.Count != request.KisiSayisi)
        {
            throw new BaseException("Konaklayan cinsiyet sayisi kisi sayisi ile uyumlu olmalidir.", 400);
        }

        if (request.TekKisilikFiyatUygulansinMi && request.KisiSayisi != 1)
        {
            throw new BaseException("Tek kisilik fiyat yalnizca tek konaklayan icin secilebilir.", 400);
        }
    }

    private static void ValidateSaveRequest(RezervasyonKaydetRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        if (request.MisafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (request.KonaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (request.GirisTarihi >= request.CikisTarihi)
        {
            throw new BaseException("Giris tarihi cikis tarihinden kucuk olmalidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.MisafirAdiSoyadi))
        {
            throw new BaseException("Misafir adi soyadi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.MisafirTelefon))
        {
            throw new BaseException("Misafir telefon zorunludur.", 400);
        }

        if (request.Segmentler.Count == 0)
        {
            throw new BaseException("En az bir rezervasyon segmenti gereklidir.", 400);
        }

        if (request.ToplamUcret < 0)
        {
            throw new BaseException("Toplam ucret sifirdan kucuk olamaz.", 400);
        }

        if (request.ToplamBazUcret < 0)
        {
            throw new BaseException("Toplam baz ucret sifirdan kucuk olamaz.", 400);
        }

        if (request.ToplamBazUcret > 0 && request.ToplamUcret > request.ToplamBazUcret)
        {
            throw new BaseException("Toplam ucret, baz ucretten buyuk olamaz.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ParaBirimi) || request.ParaBirimi.Trim().Length != 3)
        {
            throw new BaseException("Para birimi 3 karakter olmali (ornek: TRY).", 400);
        }

        if (request.MisafirCinsiyeti is not null && NormalizeKonaklayanCinsiyet(request.MisafirCinsiyeti) is null)
        {
            throw new BaseException("Misafir cinsiyeti gecersiz.", 400);
        }

        if (request.TekKisilikFiyatUygulansinMi && request.KisiSayisi != 1)
        {
            throw new BaseException("Tek kisilik fiyat yalnizca tek konaklayan icin secilebilir.", 400);
        }

        if (request.UygulananIndirimler.Any(x =>
                x.IndirimKuraliId < 0
                || string.IsNullOrWhiteSpace(x.KuralAdi)
                || x.IndirimTutari < 0
                || x.SonrasiTutar < 0))
        {
            throw new BaseException("Uygulanan indirim kayitlari gecersiz.", 400);
        }

        var orderedSegments = request.Segmentler
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.BitisTarihi)
            .ToList();

        DateTime? previousEnd = null;
        foreach (var segment in orderedSegments)
        {
            if (segment.BaslangicTarihi >= segment.BitisTarihi)
            {
                throw new BaseException("Segment baslangic tarihi segment bitis tarihinden kucuk olmalidir.", 400);
            }

            if (segment.BaslangicTarihi < request.GirisTarihi || segment.BitisTarihi > request.CikisTarihi)
            {
                throw new BaseException("Segment araliklari rezervasyon araligi disina cikamaz.", 400);
            }

            if (previousEnd.HasValue && segment.BaslangicTarihi < previousEnd.Value)
            {
                throw new BaseException("Segment araliklari birbiriyle cakisamaz.", 400);
            }

            if (segment.OdaAtamalari.Count == 0)
            {
                throw new BaseException("Her segmentte en az bir oda atamasi olmalidir.", 400);
            }

            var peopleSum = segment.OdaAtamalari.Sum(x => x.AyrilanKisiSayisi);
            if (peopleSum != request.KisiSayisi)
            {
                throw new BaseException("Her segmentte ayrilan toplam kisi sayisi rezervasyon kisi sayisina esit olmalidir.", 400);
            }

            previousEnd = segment.BitisTarihi;
        }
    }

    private async Task ValidateAppliedDiscountPermissionsAsync(RezervasyonKaydetRequestDto request, CancellationToken cancellationToken)
    {
        if (request.UygulananIndirimler.Count == 0)
        {
            return;
        }

        var customDiscounts = request.UygulananIndirimler
            .Where(x => x.IndirimKuraliId == 0)
            .ToList();

        if (customDiscounts.Count > 0)
        {
            if (!HasPermission(StructurePermissions.RezervasyonYonetimi.CustomIndirimGirebilir))
            {
                throw new BaseException("Rezervasyon custom indirimi icin yetkiniz bulunmuyor.", 403);
            }

            if (customDiscounts.Any(x => x.IndirimTutari <= 0))
            {
                throw new BaseException("Custom indirim tutari sifirdan buyuk olmalidir.", 400);
            }
        }

        var ruleIds = request.UygulananIndirimler
            .Where(x => x.IndirimKuraliId > 0)
            .Select(x => x.IndirimKuraliId)
            .Distinct()
            .ToList();

        if (ruleIds.Count == 0)
        {
            return;
        }

        var existingRuleIds = await _stysDbContext.IndirimKurallari
            .Where(x => ruleIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingRuleIds.Count != ruleIds.Count)
        {
            throw new BaseException("Uygulanan indirim kayitlari gecersiz.", 400);
        }
    }

    private async Task TryApplyPriceDecreaseAfterRoomChangeAsync(
        Rezervasyon reservation,
        IReadOnlyDictionary<int, List<RezervasyonSegmentOdaAtama>> assignmentsBySegment,
        IReadOnlyDictionary<int, int> finalRoomByAssignmentId,
        CancellationToken cancellationToken)
    {
        if (!reservation.MisafirTipiId.HasValue || !reservation.KonaklamaTipiId.HasValue)
        {
            return;
        }

        var segmentDtos = assignmentsBySegment
            .Select(group =>
            {
                var segment = group.Value[0].RezervasyonSegment
                              ?? throw new BaseException("Rezervasyon segment bilgisi eksik.", 400);

                return new SenaryoFiyatHesaplaSegmentDto
                {
                    BaslangicTarihi = segment.BaslangicTarihi,
                    BitisTarihi = segment.BitisTarihi,
                    OdaAtamalari = group.Value
                        .Select(a => new SenaryoFiyatHesaplaOdaAtamaDto
                        {
                            OdaId = finalRoomByAssignmentId[a.Id],
                            AyrilanKisiSayisi = a.AyrilanKisiSayisi
                        })
                        .ToList()
                };
            })
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.BitisTarihi)
            .ToList();

        if (segmentDtos.Count == 0)
        {
            return;
        }

        var mevcutIndirimler = DeserializeAppliedDiscounts(reservation.UygulananIndirimlerJson);
        var seciliKuralIds = mevcutIndirimler
            .Where(x => x.IndirimKuraliId > 0)
            .Select(x => x.IndirimKuraliId)
            .Distinct()
            .ToList();
        var customIndirimToplami = mevcutIndirimler
            .Where(x => x.IndirimKuraliId <= 0 && x.IndirimTutari > 0)
            .Sum(x => x.IndirimTutari);

        var fiyatSonucu = await CalculateScenarioPriceAsync(
            reservation.TesisId,
            reservation.MisafirTipiId.Value,
            reservation.KonaklamaTipiId.Value,
            reservation.KisiSayisi,
            reservation.TekKisilikFiyatUygulandiMi,
            reservation.GirisTarihi,
            reservation.CikisTarihi,
            segmentDtos,
            seciliKuralIds,
            cancellationToken);

        var nihaiTutar = fiyatSonucu.ToplamNihaiUcret;
        var guncelIndirimler = new List<UygulananIndirimDto>(fiyatSonucu.UygulananIndirimler);

        if (customIndirimToplami > 0)
        {
            var uygulanacakCustomIndirim = Math.Min(customIndirimToplami, nihaiTutar);
            if (uygulanacakCustomIndirim > 0)
            {
                nihaiTutar -= uygulanacakCustomIndirim;
                guncelIndirimler.Add(new UygulananIndirimDto
                {
                    IndirimKuraliId = 0,
                    KuralAdi = "Custom Indirim",
                    IndirimTutari = uygulanacakCustomIndirim,
                    SonrasiTutar = nihaiTutar
                });
            }
        }

        if (nihaiTutar >= reservation.ToplamUcret)
        {
            return;
        }

        reservation.ToplamBazUcret = Math.Min(reservation.ToplamBazUcret, fiyatSonucu.ToplamBazUcret);
        reservation.ToplamUcret = nihaiTutar;
        reservation.UygulananIndirimlerJson = SerializeAppliedDiscounts(guncelIndirimler);
    }

    private bool HasPermission(string permission)
    {
        var claims = _httpContextAccessor.HttpContext?.User
            .FindAll(TodPlatformAuthorizationConstants.PermissionClaimType)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        if (claims is null)
        {
            return false;
        }

        return claims.Any(x => x.Equals(permission, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeOdemeTipi(string? odemeTipi)
    {
        if (string.IsNullOrWhiteSpace(odemeTipi))
        {
            throw new BaseException("Odeme tipi zorunludur.", 400);
        }

        if (odemeTipi.Equals(OdemeTipleri.Nakit, StringComparison.OrdinalIgnoreCase))
        {
            return OdemeTipleri.Nakit;
        }

        if (odemeTipi.Equals(OdemeTipleri.KrediKarti, StringComparison.OrdinalIgnoreCase))
        {
            return OdemeTipleri.KrediKarti;
        }

        throw new BaseException("Gecersiz odeme tipi.", 400);
    }

    private async Task AcquireRoomApplicationLocksAsync(
        IReadOnlyCollection<int> roomIds,
        CancellationToken cancellationToken)
    {
        if (roomIds.Count == 0 || !_stysDbContext.Database.IsRelational())
        {
            return;
        }

        foreach (var roomId in roomIds.Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            var resource = $"stys:rezervasyon:oda:{roomId}";
            var lockResults = await _stysDbContext.Database
                .SqlQueryRaw<int>(
                    "DECLARE @result int; EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {1}; SELECT @result;",
                    resource,
                    15000)
                .ToListAsync(cancellationToken);
            var lockResult = lockResults.Single();

            if (lockResult < 0)
            {
                throw new BaseException($"'{roomId}' numarali oda icin kilit alinamadi. Lutfen islemi tekrar deneyin.", 409);
            }
        }
    }

    private static void EnsureCanChangeRoomForReservationStatus(string rezervasyonDurumu)
    {
        if (rezervasyonDurumu == RezervasyonDurumlari.Taslak
            || rezervasyonDurumu == RezervasyonDurumlari.Onayli
            || rezervasyonDurumu == RezervasyonDurumlari.CheckInTamamlandi)
        {
            return;
        }

        throw new BaseException("Bu rezervasyon durumu icin oda degisimi yapilamaz.", 400);
    }

    private async Task ReassignGuestSegmentAssignmentsAfterRoomChangeAsync(
        int rezervasyonId,
        IReadOnlyCollection<OdaDegisimAppliedChange> changedAssignments,
        IReadOnlyDictionary<int, OdaDegisimRoomInfo> roomInfoById,
        CancellationToken cancellationToken)
    {
        if (changedAssignments.Count == 0)
        {
            return;
        }

        foreach (var changedAssignment in changedAssignments)
        {
            var movedGuestAssignments = await (
                    from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                    join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                    where konaklayan.RezervasyonId == rezervasyonId
                          && atama.RezervasyonSegmentId == changedAssignment.SegmentId
                          && atama.OdaId == changedAssignment.EskiOdaId
                    orderby konaklayan.SiraNo, atama.Id
                    select atama)
                .ToListAsync(cancellationToken);

            if (movedGuestAssignments.Count == 0)
            {
                continue;
            }

            var roomInfo = roomInfoById[changedAssignment.YeniOdaId];
            if (!roomInfo.PaylasimliMi)
            {
                foreach (var guestAssignment in movedGuestAssignments)
                {
                    guestAssignment.OdaId = changedAssignment.YeniOdaId;
                    guestAssignment.YatakNo = null;
                }

                continue;
            }

            var occupiedBeds = await (
                    from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                    join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                    join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                    where atama.OdaId == changedAssignment.YeniOdaId
                          && atama.YatakNo.HasValue
                          && segment.BaslangicTarihi < changedAssignment.BitisTarihi
                          && segment.BitisTarihi > changedAssignment.BaslangicTarihi
                          && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                          && konaklayan.RezervasyonId != rezervasyonId
                    select atama.YatakNo!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var availableBeds = Enumerable.Range(1, roomInfo.Kapasite)
                .Except(occupiedBeds)
                .OrderBy(x => x)
                .ToList();

            if (availableBeds.Count < movedGuestAssignments.Count)
            {
                throw new BaseException($"'{roomInfo.OdaNo}' odasi icin konaklayan yatak atamasi yapilamadi.", 400);
            }

            foreach (var guestAssignment in movedGuestAssignments)
            {
                guestAssignment.OdaId = changedAssignment.YeniOdaId;
            }

            foreach (var guestAssignment in movedGuestAssignments.Where(x => x.YatakNo.HasValue).OrderBy(x => x.Id))
            {
                if (guestAssignment.YatakNo.HasValue && availableBeds.Remove(guestAssignment.YatakNo.Value))
                {
                    continue;
                }

                guestAssignment.YatakNo = null;
            }

            foreach (var guestAssignment in movedGuestAssignments.Where(x => !x.YatakNo.HasValue).OrderBy(x => x.Id))
            {
                var nextBed = availableBeds[0];
                availableBeds.RemoveAt(0);
                guestAssignment.YatakNo = nextBed;
            }
        }
    }

    private async Task<List<OdaDegisimAssignmentInfo>> GetReservationSegmentAssignmentsAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        return await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                where segment.RezervasyonId == rezervasyonId
                orderby segment.SegmentSirasi, atama.Id
                select new OdaDegisimAssignmentInfo(
                    atama.Id,
                    segment.Id,
                    segment.SegmentSirasi,
                    segment.BaslangicTarihi,
                    segment.BitisTarihi,
                    atama.AyrilanKisiSayisi,
                    atama.OdaId,
                    atama.OdaNoSnapshot,
                    atama.BinaAdiSnapshot,
                    atama.OdaTipiAdiSnapshot,
                    atama.PaylasimliMiSnapshot,
                    atama.KapasiteSnapshot))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RezervasyonOdaDegisimAdayOdaDto>> GetReplacementCandidatesForAssignmentAsync(
        int rezervasyonId,
        int tesisId,
        OdaDegisimAssignmentInfo assignment,
        IReadOnlyCollection<OdaDegisimAssignmentInfo> segmentAssignments,
        IReadOnlyCollection<string?> movedGuestGenders,
        CancellationToken cancellationToken)
    {
        var candidateRooms = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
                where oda.AktifMi
                      && bina.AktifMi
                      && odaTipi.AktifMi
                      && bina.TesisId == tesisId
                      && odaTipi.Kapasite >= assignment.AyrilanKisiSayisi
                select new OdaDegisimRoomInfo(
                    oda.Id,
                    oda.OdaNo,
                    bina.Ad,
                    odaTipi.Ad,
                    odaTipi.Kapasite,
                    odaTipi.PaylasimliMi))
            .ToListAsync(cancellationToken);

        if (candidateRooms.Count == 0)
        {
            return [];
        }

        var roomIds = candidateRooms
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        var blockedRoomIds = await _stysDbContext.OdaKullanimBloklari
            .Where(x =>
                x.AktifMi
                && roomIds.Contains(x.OdaId)
                && x.BaslangicTarihi < assignment.BitisTarihi
                && x.BitisTarihi > assignment.BaslangicTarihi)
            .Select(x => x.OdaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var blockedRoomSet = blockedRoomIds.ToHashSet();

        var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
            roomIds,
            assignment.BaslangicTarihi,
            assignment.BitisTarihi,
            cancellationToken,
            rezervasyonId);
        var externalSharedRoomOccupancies = await GetSharedRoomGuestOccupanciesAsync(
            roomIds,
            assignment.BaslangicTarihi,
            assignment.BitisTarihi,
            cancellationToken,
            rezervasyonId);
        var occupiedBedsByRoom = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                where roomIds.Contains(atama.OdaId)
                      && atama.YatakNo.HasValue
                      && segment.BaslangicTarihi < assignment.BitisTarihi
                      && segment.BitisTarihi > assignment.BaslangicTarihi
                      && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                      && konaklayan.RezervasyonId != rezervasyonId
                select new
                {
                    atama.OdaId,
                    YatakNo = atama.YatakNo!.Value
                })
            .ToListAsync(cancellationToken);
        var occupiedBedSetByRoom = occupiedBedsByRoom
            .GroupBy(x => x.OdaId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.YatakNo).Distinct().ToHashSet());

        var segmentOccupiedByOtherAssignments = segmentAssignments
            .Where(x => x.RezervasyonSegmentOdaAtamaId != assignment.RezervasyonSegmentOdaAtamaId)
            .GroupBy(x => x.OdaId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.AyrilanKisiSayisi));
        var hasMovingGuestAssignments = movedGuestGenders.Count > 0;
        string? movingGuestGender = null;
        var hasCompatibleMovingGuestGender = TryResolveSharedRoomGuestGender(movedGuestGenders, out movingGuestGender);

        return candidateRooms
            .Where(room => !blockedRoomSet.Contains(room.OdaId))
            .Select(room =>
            {
                var occupiedByOthers = occupancyByRoom.TryGetValue(room.OdaId, out var occupied) ? occupied : 0;
                var occupiedInSegment = segmentOccupiedByOtherAssignments.TryGetValue(room.OdaId, out var segmentOccupied) ? segmentOccupied : 0;
                var totalOccupied = occupiedByOthers + occupiedInSegment;
                var roomGenderSet = room.PaylasimliMi
                    ? GetDistinctSharedRoomGenders(
                        externalSharedRoomOccupancies,
                        room.OdaId,
                        assignment.BaslangicTarihi,
                        assignment.BitisTarihi)
                    : [];
                var remainingCapacity = room.PaylasimliMi
                    ? Math.Max(0, room.Kapasite - totalOccupied)
                    : totalOccupied > 0
                        ? 0
                        : room.Kapasite;
                if (room.PaylasimliMi)
                {
                    if (hasMovingGuestAssignments && !hasCompatibleMovingGuestGender)
                    {
                        remainingCapacity = 0;
                    }
                    else if (totalOccupied > 0 && roomGenderSet.Count == 0)
                    {
                        remainingCapacity = 0;
                    }
                    else if (hasMovingGuestAssignments && roomGenderSet.Count > 1)
                    {
                        remainingCapacity = 0;
                    }
                    else if (hasMovingGuestAssignments
                             && roomGenderSet.Count == 1
                             && !string.Equals(roomGenderSet.Single(), movingGuestGender, StringComparison.OrdinalIgnoreCase))
                    {
                        remainingCapacity = 0;
                    }
                }

                var recommendedBeds = room.PaylasimliMi
                    ? Enumerable.Range(1, room.Kapasite)
                        .Where(x => !occupiedBedSetByRoom.TryGetValue(room.OdaId, out var occupiedBeds) || !occupiedBeds.Contains(x))
                        .Take(assignment.AyrilanKisiSayisi)
                        .ToList()
                    : [];

                return new RezervasyonOdaDegisimAdayOdaDto
                {
                    OdaId = room.OdaId,
                    OdaNo = room.OdaNo,
                    BinaAdi = room.BinaAdi,
                    OdaTipiAdi = room.OdaTipiAdi,
                    PaylasimliMi = room.PaylasimliMi,
                    Kapasite = room.Kapasite,
                    KalanKapasite = remainingCapacity,
                    OnerilenYatakNolari = recommendedBeds
                };
            })
            .Where(x => x.KalanKapasite >= assignment.AyrilanKisiSayisi)
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ThenBy(x => x.OdaId)
            .ToList();
    }

    private async Task<Dictionary<int, int>> GetCurrentOccupancyByRoomAsync(
        IReadOnlyCollection<int> roomIds,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken,
        int? excludeRezervasyonId = null)
    {
        if (roomIds.Count == 0)
        {
            return [];
        }

        var overlaps = await (
            from atama in _stysDbContext.RezervasyonSegmentOdaAtamalari
            join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
            join rezervasyon in _stysDbContext.Rezervasyonlar on segment.RezervasyonId equals rezervasyon.Id
            where rezervasyon.AktifMi
                  && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                  && (!excludeRezervasyonId.HasValue || rezervasyon.Id != excludeRezervasyonId.Value)
                  && roomIds.Contains(atama.OdaId)
                  && segment.BaslangicTarihi < bitis
                  && segment.BitisTarihi > baslangic
            select new
            {
                RezervasyonId = rezervasyon.Id,
                SegmentId = segment.Id,
                atama.OdaId,
                atama.AyrilanKisiSayisi
            })
            .ToListAsync(cancellationToken);

        if (overlaps.Count == 0)
        {
            return [];
        }

        var reservationIds = overlaps
            .Select(x => x.RezervasyonId)
            .Distinct()
            .ToList();

        var guestCountByReservation = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => reservationIds.Contains(x.RezervasyonId))
            .GroupBy(x => x.RezervasyonId)
            .Select(x => new
            {
                RezervasyonId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(x => x.RezervasyonId, x => x.Count, cancellationToken);

        var actualOccupancyByAssignment = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                where reservationIds.Contains(konaklayan.RezervasyonId)
                      && roomIds.Contains(atama.OdaId)
                      && segment.BaslangicTarihi < bitis
                      && segment.BitisTarihi > baslangic
                      && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                group atama by new
                {
                    konaklayan.RezervasyonId,
                    atama.RezervasyonSegmentId,
                    atama.OdaId
                }
                into grouped
                select new
                {
                    grouped.Key.RezervasyonId,
                    SegmentId = grouped.Key.RezervasyonSegmentId,
                    grouped.Key.OdaId,
                    Count = grouped.Count()
                })
            .ToListAsync(cancellationToken);

        var actualOccupancyLookup = actualOccupancyByAssignment.ToDictionary(
            x => (x.RezervasyonId, x.SegmentId, x.OdaId),
            x => x.Count);

        return overlaps
            .GroupBy(x => x.OdaId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(x =>
                {
                    var hasPlan = guestCountByReservation.TryGetValue(x.RezervasyonId, out var guestCount) && guestCount > 0;
                    return hasPlan
                        ? actualOccupancyLookup.GetValueOrDefault((x.RezervasyonId, x.SegmentId, x.OdaId))
                        : x.AyrilanKisiSayisi;
                }));
    }

    private async Task<List<SharedRoomGuestOccupancy>> GetSharedRoomGuestOccupanciesAsync(
        IReadOnlyCollection<int> roomIds,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken,
        int? excludeRezervasyonId = null)
    {
        if (roomIds.Count == 0)
        {
            return [];
        }

        var occupancies = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                join rezervasyon in _stysDbContext.Rezervasyonlar on konaklayan.RezervasyonId equals rezervasyon.Id
                where rezervasyon.AktifMi
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                      && (!excludeRezervasyonId.HasValue || rezervasyon.Id != excludeRezervasyonId.Value)
                      && roomIds.Contains(atama.OdaId)
                      && segment.BaslangicTarihi < bitis
                      && segment.BitisTarihi > baslangic
                select new
                {
                    atama.OdaId,
                    segment.BaslangicTarihi,
                    segment.BitisTarihi,
                    konaklayan.Cinsiyet
                })
            .ToListAsync(cancellationToken);

        return occupancies
            .Select(x => new SharedRoomGuestOccupancy(
                x.OdaId,
                x.BaslangicTarihi,
                x.BitisTarihi,
                NormalizeStoredKonaklayanCinsiyet(x.Cinsiyet)))
            .Where(x => x.Cinsiyet is not null)
            .ToList()!;
    }

    private static HashSet<string> GetDistinctSharedRoomGenders(
        IEnumerable<SharedRoomGuestOccupancy> occupancies,
        int odaId,
        DateTime baslangic,
        DateTime bitis)
    {
        return occupancies
            .Where(x => x.OdaId == odaId && x.BaslangicTarihi < bitis && x.BitisTarihi > baslangic && x.Cinsiyet is not null)
            .Select(x => x.Cinsiyet!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static ScenarioGuestGenderRequirements BuildScenarioGuestGenderRequirements(
        IReadOnlyCollection<string?> guestGenders,
        int kisiSayisi)
    {
        if (guestGenders.Count == 0)
        {
            return ScenarioGuestGenderRequirements.None(kisiSayisi);
        }

        var normalizedGenders = guestGenders
            .Select(NormalizeKonaklayanCinsiyet)
            .ToList();

        if (normalizedGenders.Count != kisiSayisi)
        {
            throw new BaseException("Konaklayan cinsiyet sayisi kisi sayisi ile uyumlu olmalidir.", 400);
        }

        if (normalizedGenders.Any(x => x is null))
        {
            throw new BaseException("Senaryo aramasi icin tum konaklayanlarin cinsiyeti zorunludur.", 400);
        }

        var kadinSayisi = normalizedGenders.Count(x => string.Equals(x, KonaklayanCinsiyetleri.Kadin, StringComparison.OrdinalIgnoreCase));
        var erkekSayisi = normalizedGenders.Count(x => string.Equals(x, KonaklayanCinsiyetleri.Erkek, StringComparison.OrdinalIgnoreCase));
        return new ScenarioGuestGenderRequirements(kadinSayisi, erkekSayisi, kisiSayisi);
    }

    private static bool TryResolveSharedRoomGuestGender(
        IEnumerable<string?> guestGenders,
        out string? resolvedGender)
    {
        resolvedGender = null;
        var normalizedGenders = guestGenders
            .Select(NormalizeStoredKonaklayanCinsiyet)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasUnknownGender = guestGenders.Any(x => string.IsNullOrWhiteSpace(x) || NormalizeStoredKonaklayanCinsiyet(x) is null);
        if (hasUnknownGender || normalizedGenders.Count != 1)
        {
            return false;
        }

        resolvedGender = normalizedGenders[0];
        return true;
    }

    private static string? NormalizeKonaklayanCinsiyet(string? cinsiyet)
    {
        if (string.IsNullOrWhiteSpace(cinsiyet))
        {
            return null;
        }

        var normalized = NormalizeStoredKonaklayanCinsiyet(cinsiyet);
        if (normalized is null)
        {
            throw new BaseException("Konaklayan cinsiyeti gecersiz.", 400);
        }

        return normalized;
    }

    private static string? NormalizeStoredKonaklayanCinsiyet(string? cinsiyet)
    {
        if (string.IsNullOrWhiteSpace(cinsiyet))
        {
            return null;
        }

        if (string.Equals(cinsiyet, KonaklayanCinsiyetleri.Kadin, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanCinsiyetleri.Kadin;
        }

        if (string.Equals(cinsiyet, KonaklayanCinsiyetleri.Erkek, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanCinsiyetleri.Erkek;
        }

        return null;
    }

    private static string NormalizeKonaklayanKatilimDurumu(string? katilimDurumu)
    {
        return NormalizeStoredKonaklayanKatilimDurumu(katilimDurumu);
    }

    private static string NormalizeStoredKonaklayanKatilimDurumu(string? katilimDurumu)
    {
        if (string.IsNullOrWhiteSpace(katilimDurumu))
        {
            return KonaklayanKatilimDurumlari.Bekleniyor;
        }

        if (string.Equals(katilimDurumu, KonaklayanKatilimDurumlari.Bekleniyor, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanKatilimDurumlari.Bekleniyor;
        }

        if (string.Equals(katilimDurumu, KonaklayanKatilimDurumlari.Geldi, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanKatilimDurumlari.Geldi;
        }

        if (string.Equals(katilimDurumu, KonaklayanKatilimDurumlari.Gelmedi, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanKatilimDurumlari.Gelmedi;
        }

        if (string.Equals(katilimDurumu, KonaklayanKatilimDurumlari.Ayrildi, StringComparison.OrdinalIgnoreCase))
        {
            return KonaklayanKatilimDurumlari.Ayrildi;
        }

        throw new BaseException("Konaklayan katilim durumu gecersiz.", 400);
    }

    private static SelectedScenarioPrice ResolveScenarioPrice(
        IReadOnlyCollection<OdaFiyat> priceRows,
        bool paylasimliOdaTipi,
        int segmentKisiSayisi,
        int rezervasyonKisiSayisi,
        bool tekKisilikFiyatUygulansinMi,
        DateTime hedefTarih)
    {
        var kisiBasiPrice = priceRows.FirstOrDefault(x => x.KullanimSekli.Equals(OdaFiyatKullanimSekilleri.KisiBasi, StringComparison.OrdinalIgnoreCase));
        var ozelKullanimPrice = priceRows.FirstOrDefault(x => x.KullanimSekli.Equals(OdaFiyatKullanimSekilleri.OzelKullanim, StringComparison.OrdinalIgnoreCase));

        if (paylasimliOdaTipi)
        {
            if (kisiBasiPrice is null)
            {
                throw new BaseException($"{hedefTarih:yyyy-MM-dd} tarihi icin paylasimli kullanim kisi bazli tarife bulunamadi.", 400);
            }

            return new SelectedScenarioPrice(kisiBasiPrice.ParaBirimi, kisiBasiPrice.Fiyat * segmentKisiSayisi);
        }

        if (tekKisilikFiyatUygulansinMi && rezervasyonKisiSayisi == 1)
        {
            if (kisiBasiPrice is null)
            {
                throw new BaseException($"{hedefTarih:yyyy-MM-dd} tarihi icin tek kisilik fiyat uygulanacak kisi bazli tarife bulunamadi.", 400);
            }

            return new SelectedScenarioPrice(kisiBasiPrice.ParaBirimi, kisiBasiPrice.Fiyat);
        }

        if (ozelKullanimPrice is not null)
        {
            return new SelectedScenarioPrice(ozelKullanimPrice.ParaBirimi, ozelKullanimPrice.Fiyat);
        }

        if (kisiBasiPrice is not null)
        {
            return new SelectedScenarioPrice(kisiBasiPrice.ParaBirimi, kisiBasiPrice.Fiyat * segmentKisiSayisi);
        }

        throw new BaseException($"{hedefTarih:yyyy-MM-dd} tarihi icin uygun oda fiyati bulunamadi.", 400);
    }

    private static string BuildRezervasyonFiyatlamaOzeti(
        bool tekKisilikFiyatUygulandiMi,
        IReadOnlyCollection<RezervasyonDetaySegmentDto> segmentler)
    {
        if (tekKisilikFiyatUygulandiMi)
        {
            return "Tek kisilik fiyat uygulandi";
        }

        var tumAtamalar = segmentler.SelectMany(x => x.OdaAtamalari).ToList();
        if (tumAtamalar.Count == 0)
        {
            return "Standart fiyatlama";
        }

        var hasShared = tumAtamalar.Any(x => x.PaylasimliMi);
        var hasPrivate = tumAtamalar.Any(x => !x.PaylasimliMi);

        if (hasShared && hasPrivate)
        {
            return "Karma fiyatlama (paylasimli + ozel kullanim)";
        }

        if (hasShared)
        {
            return "Kisi basi fiyatlama";
        }

        return "Ozel kullanim fiyatlama";
    }

    private static bool DoesGuestRequireAssignments(string katilimDurumu)
    {
        return !string.Equals(katilimDurumu, KonaklayanKatilimDurumlari.Gelmedi, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ResolveRezervasyonEkHizmetBirimFiyati(decimal? requestBirimFiyat, decimal tarifeBirimFiyat)
    {
        var birimFiyat = requestBirimFiyat ?? tarifeBirimFiyat;
        if (birimFiyat < 0m)
        {
            throw new BaseException("Ek hizmet birim fiyati sifirdan kucuk olamaz.", 400);
        }

        return Math.Round(birimFiyat, 2, MidpointRounding.AwayFromZero);
    }

    private static string GenerateReferenceNo()
    {
        return $"RZV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static string SerializeAppliedDiscounts(IReadOnlyCollection<UygulananIndirimDto> discounts)
    {
        if (discounts.Count == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(discounts);
    }

    private static List<UygulananIndirimDto> DeserializeAppliedDiscounts(string? discountsJson)
    {
        if (string.IsNullOrWhiteSpace(discountsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<UygulananIndirimDto>>(discountsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<int, decimal>> GetEkHizmetToplamlariAsync(
        IReadOnlyCollection<int> rezervasyonIds,
        CancellationToken cancellationToken)
    {
        if (rezervasyonIds.Count == 0)
        {
            return [];
        }

        return await _stysDbContext.RezervasyonEkHizmetler
            .Where(x => rezervasyonIds.Contains(x.RezervasyonId))
            .GroupBy(x => x.RezervasyonId)
            .Select(group => new
            {
                RezervasyonId = group.Key,
                Toplam = group.Sum(x => x.ToplamTutar)
            })
            .ToDictionaryAsync(x => x.RezervasyonId, x => x.Toplam, cancellationToken);
    }

    private async Task<decimal> GetRezervasyonEkHizmetToplamiAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.RezervasyonEkHizmetler
            .Where(x => x.RezervasyonId == rezervasyonId)
            .Select(x => (decimal?)x.ToplamTutar)
            .SumAsync(cancellationToken) ?? 0m;
    }

    private async Task<List<RezervasyonEkHizmetDto>> GetEkHizmetlerAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.RezervasyonEkHizmetler
            .Where(x => x.RezervasyonId == rezervasyonId)
            .OrderByDescending(x => x.HizmetTarihi)
            .ThenByDescending(x => x.Id)
            .Select(x => new RezervasyonEkHizmetDto
            {
                Id = x.Id,
                RezervasyonKonaklayanId = x.RezervasyonKonaklayanId,
                EkHizmetId = x.EkHizmetId,
                EkHizmetTarifeId = x.EkHizmetTarifeId,
                KonaklayanAdiSoyadi = x.RezervasyonKonaklayan != null ? x.RezervasyonKonaklayan.AdSoyad : string.Empty,
                TarifeAdi = x.TarifeAdiSnapshot,
                HizmetTarihi = x.HizmetTarihi,
                Miktar = x.Miktar,
                BirimAdi = x.BirimAdiSnapshot,
                BirimFiyat = x.BirimFiyat,
                ToplamTutar = x.ToplamTutar,
                ParaBirimi = x.ParaBirimi,
                OdaNo = x.OdaNoSnapshot,
                BinaAdi = x.BinaAdiSnapshot,
                YatakNo = x.YatakNoSnapshot,
                Aciklama = x.Aciklama
            })
            .ToListAsync(cancellationToken);
    }

    private static Task EnsureReservationAllowsPaymentCreateAsync(Rezervasyon reservation)
    {
        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyona odeme eklenemez.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Odeme almak icin once check-in tamamlanmalidir.", 400);
        }

        return Task.CompletedTask;
    }

    private static Task EnsureReservationAllowsExtraServiceCreateAsync(Rezervasyon reservation)
    {
        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyona ek hizmet eklenemez.", 400);
        }

        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi)
        {
            throw new BaseException("Check-out tamamlanan rezervasyona ek hizmet eklenemez.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Ek hizmet eklemek icin once check-in tamamlanmalidir.", 400);
        }

        return Task.CompletedTask;
    }

    private async Task EnsureReservationAllowsExtraServiceEditAsync(Rezervasyon reservation, CancellationToken cancellationToken)
    {
        await EnsureReservationAllowsExtraServiceCreateAsync(reservation);
    }

    private async Task EnsureExtraServiceDeletionAllowedAsync(int rezervasyonId, decimal silinecekTutar, CancellationToken cancellationToken)
    {
        var odenenTutar = await GetToplamOdenenTutarAsync(rezervasyonId, cancellationToken);
        var mevcutEkHizmetToplami = await GetRezervasyonEkHizmetToplamiAsync(rezervasyonId, cancellationToken);
        var rezervasyonTutari = await GetRezervasyonKonaklamaTutariAsync(rezervasyonId, cancellationToken);
        var mevcutToplamTutar = rezervasyonTutari + mevcutEkHizmetToplami;
        var mevcutKalanTutar = mevcutToplamTutar - odenenTutar;

        if (mevcutKalanTutar <= 0)
        {
            throw new BaseException("Odeme bakiyesi sifirlanmis rezervasyonda ek hizmet silinemez.", 400);
        }

        var yeniToplamTutar = mevcutToplamTutar - silinecekTutar;
        if (yeniToplamTutar < odenenTutar)
        {
            throw new BaseException("Ek hizmet silinirse odenmis tutar rezervasyon toplamindan buyuk kalir.", 400);
        }
    }

    private async Task EnsureExtraServiceAdjustmentKeepsBalanceNonNegativeAsync(
        int rezervasyonId,
        decimal mevcutTutar,
        decimal yeniTutar,
        CancellationToken cancellationToken)
    {
        if (yeniTutar >= mevcutTutar)
        {
            return;
        }

        var odenenTutar = await GetToplamOdenenTutarAsync(rezervasyonId, cancellationToken);
        var mevcutEkHizmetToplami = await GetRezervasyonEkHizmetToplamiAsync(rezervasyonId, cancellationToken);
        var rezervasyonTutari = await GetRezervasyonKonaklamaTutariAsync(rezervasyonId, cancellationToken);
        var mevcutToplamTutar = rezervasyonTutari + mevcutEkHizmetToplami;
        var mevcutKalanTutar = mevcutToplamTutar - odenenTutar;

        if (mevcutKalanTutar <= 0)
        {
            throw new BaseException("Odeme bakiyesi sifirlanmis rezervasyonda ek hizmet tutari dusurulemez.", 400);
        }

        var yeniToplamTutar = mevcutToplamTutar - mevcutTutar + yeniTutar;
        if (yeniToplamTutar < odenenTutar)
        {
            throw new BaseException("Ek hizmet guncellemesi sonrasinda odenmis tutar rezervasyon toplamindan buyuk kalir.", 400);
        }
    }

    private async Task<decimal> GetToplamOdenenTutarAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == rezervasyonId)
            .Select(x => (decimal?)x.OdemeTutari)
            .SumAsync(cancellationToken) ?? 0m;
    }

    private async Task<decimal> GetRezervasyonKonaklamaTutariAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId)
            .Select(x => (decimal?)x.ToplamUcret)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;
    }

    private async Task<(EkHizmetTarife Tarife, RezervasyonKonaklayan Konaklayan, RezervasyonSegment Segment, RezervasyonKonaklayanSegmentAtama KonaklayanAtama, RezervasyonSegmentOdaAtama OdaAtama)> ResolveEkHizmetContextAsync(
        Rezervasyon reservation,
        RezervasyonEkHizmetKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.RezervasyonKonaklayanId <= 0)
        {
            throw new BaseException("Konaklayan secimi zorunludur.", 400);
        }

        if (request.EkHizmetTarifeId <= 0)
        {
            throw new BaseException("Ek hizmet secimi zorunludur.", 400);
        }

        if (request.Miktar <= 0)
        {
            throw new BaseException("Miktar sifirdan buyuk olmalidir.", 400);
        }

        var hizmetTarihi = request.HizmetTarihi;
        if (hizmetTarihi < reservation.GirisTarihi || hizmetTarihi > reservation.CikisTarihi)
        {
            throw new BaseException("Hizmet tarihi rezervasyon araliginda olmalidir.", 400);
        }

        var tarife = await _stysDbContext.EkHizmetTarifeleri
            .Include(x => x.EkHizmet)
            .FirstOrDefaultAsync(
                x => x.Id == request.EkHizmetTarifeId
                    && x.TesisId == reservation.TesisId
                    && x.AktifMi
                    && x.EkHizmet != null
                    && x.EkHizmet.AktifMi,
                cancellationToken);

        if (tarife is null)
        {
            throw new BaseException("Ek hizmet tarifesi bulunamadi.", 404);
        }

        if (hizmetTarihi.Date < tarife.BaslangicTarihi.Date || hizmetTarihi.Date > tarife.BitisTarihi.Date)
        {
            throw new BaseException("Secilen hizmet tarihi icin ek hizmet tarifesi gecersiz.", 400);
        }

        if (!tarife.ParaBirimi.Equals(reservation.ParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Rezervasyon para birimi ile ek hizmet para birimi ayni olmalidir.", 400);
        }

        if (tarife.EkHizmet is null)
        {
            throw new BaseException("Ek hizmet tanimi bulunamadi.", 404);
        }

        var konaklayan = await _stysDbContext.RezervasyonKonaklayanlar
            .Include(x => x.SegmentAtamalari)
                .ThenInclude(x => x.RezervasyonSegment)
                    .ThenInclude(x => x!.OdaAtamalari)
            .FirstOrDefaultAsync(
                x => x.Id == request.RezervasyonKonaklayanId && x.RezervasyonId == reservation.Id,
                cancellationToken);

        if (konaklayan is null)
        {
            throw new BaseException("Secilen konaklayan rezervasyona ait degil.", 400);
        }

        if (!string.Equals(
                NormalizeKonaklayanKatilimDurumu(konaklayan.KatilimDurumu),
                KonaklayanKatilimDurumlari.Geldi,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Ek hizmet yalnizca fiilen gelen konaklayana eklenebilir.", 400);
        }

        var aktifAtama = konaklayan.SegmentAtamalari
            .Where(x => x.RezervasyonSegment is not null)
            .Select(x => new
            {
                Atama = x,
                Segment = x.RezervasyonSegment!,
                OdaAtama = x.RezervasyonSegment!.OdaAtamalari.FirstOrDefault(o => o.OdaId == x.OdaId)
            })
            .FirstOrDefault(x => x.Segment.BaslangicTarihi <= hizmetTarihi && x.Segment.BitisTarihi > hizmetTarihi);

        if (aktifAtama is null || aktifAtama.OdaAtama is null)
        {
            throw new BaseException("Hizmet tarihi icin konaklayanin oda/yatak atamasi bulunamadi. Once konaklayan plani kaydedilmelidir.", 400);
        }

        return (tarife, konaklayan, aktifAtama.Segment, aktifAtama.Atama, aktifAtama.OdaAtama);
    }

    private void AppendHistoryEntry(
        Rezervasyon reservation,
        string islemTipi,
        string? aciklama,
        object? oncekiDeger,
        object? yeniDeger)
    {
        reservation.DegisiklikGecmisiKayitlari.Add(CreateHistoryEntry(
            islemTipi,
            aciklama,
            oncekiDeger,
            yeniDeger));
    }

    private void AppendHistoryEntry(
        int rezervasyonId,
        string islemTipi,
        string? aciklama,
        object? oncekiDeger,
        object? yeniDeger)
    {
        _stysDbContext.RezervasyonDegisiklikGecmisleri.Add(CreateHistoryEntry(
            islemTipi,
            aciklama,
            oncekiDeger,
            yeniDeger,
            rezervasyonId));
    }

    private static RezervasyonDegisiklikGecmisi CreateHistoryEntry(
        string islemTipi,
        string? aciklama,
        object? oncekiDeger,
        object? yeniDeger,
        int? rezervasyonId = null)
    {
        return new RezervasyonDegisiklikGecmisi
        {
            RezervasyonId = rezervasyonId.GetValueOrDefault(),
            IslemTipi = islemTipi,
            Aciklama = aciklama,
            OncekiDegerJson = SerializeHistoryPayload(oncekiDeger),
            YeniDegerJson = SerializeHistoryPayload(yeniDeger)
        };
    }

    private static string? SerializeHistoryPayload(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(payload);
    }

    private async Task<List<RoomAvailability>> GetRoomAvailabilitiesAsync(
        int tesisId,
        int? odaTipiId,
        int kisiSayisi,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken)
    {
        if (odaTipiId.HasValue && odaTipiId.Value > 0)
        {
            var odaTipi = await _stysDbContext.OdaTipleri
                .Where(x => x.Id == odaTipiId.Value && x.AktifMi)
                .Select(x => new { x.Id, x.TesisId })
                .FirstOrDefaultAsync(cancellationToken);

            if (odaTipi is null || odaTipi.TesisId != tesisId)
            {
                throw new BaseException("Secilen oda tipi, tesis ile uyumlu degil.", 400);
            }
        }

        var candidateRooms = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join roomType in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals roomType.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && roomType.AktifMi
                  && bina.TesisId == tesisId
                  && roomType.Kapasite >= 1
                  && (!odaTipiId.HasValue || odaTipiId.Value <= 0 || roomType.Id == odaTipiId.Value)
            select new
            {
                OdaId = oda.Id,
                oda.OdaNo,
                BinaId = bina.Id,
                BinaAdi = bina.Ad,
                OdaTipiId = roomType.Id,
                OdaTipiAdi = roomType.Ad,
                roomType.Kapasite,
                roomType.PaylasimliMi
            })
            .ToListAsync(cancellationToken);

        if (candidateRooms.Count == 0)
        {
            return [];
        }

        var candidateRoomIds = candidateRooms
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        var blockedRoomIds = await _stysDbContext.OdaKullanimBloklari
            .Where(x =>
                x.AktifMi
                && candidateRoomIds.Contains(x.OdaId)
                && x.BaslangicTarihi < bitis
                && x.BitisTarihi > baslangic)
            .Select(x => x.OdaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var blockedRoomSet = blockedRoomIds.ToHashSet();

        var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
            candidateRoomIds,
            baslangic,
            bitis,
            cancellationToken);
        var sharedRoomOccupancies = await GetSharedRoomGuestOccupanciesAsync(
            candidateRoomIds,
            baslangic,
            bitis,
            cancellationToken);

        var result = new List<RoomAvailability>();
        foreach (var room in candidateRooms)
        {
            if (blockedRoomSet.Contains(room.OdaId))
            {
                continue;
            }

            var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;
            int remaining;
            string? roomFixedGender = null;

            if (room.PaylasimliMi)
            {
                var roomGenderSet = GetDistinctSharedRoomGenders(sharedRoomOccupancies, room.OdaId, baslangic, bitis);
                roomFixedGender = roomGenderSet.Count == 1 ? roomGenderSet.Single() : null;
                if (roomGenderSet.Count > 1)
                {
                    continue;
                }
                if (occupied > 0 && roomGenderSet.Count == 0)
                {
                    continue;
                }
                if (roomFixedGender is not null
                    && !guestGenderRequirements.CanUseSharedRoomOfGender(roomFixedGender))
                {
                    continue;
                }

                remaining = Math.Max(0, room.Kapasite - occupied);
                if (guestGenderRequirements.HasMixedKnownGenders && roomFixedGender is null)
                {
                    remaining = Math.Min(remaining, guestGenderRequirements.MaxSameGenderGroupSize);
                }
            }
            else
            {
                remaining = occupied > 0 ? 0 : room.Kapasite;
            }

            if (remaining <= 0)
            {
                continue;
            }

            result.Add(new RoomAvailability(
                room.OdaId,
                room.OdaNo,
                room.BinaId,
                room.BinaAdi,
                room.OdaTipiId,
                room.OdaTipiAdi,
                room.Kapasite,
                room.PaylasimliMi,
                remaining,
                room.PaylasimliMi ? roomFixedGender : null));
        }

        return result;
    }

    private async Task<HashSet<int>> GetReservationsRequiringRoomReassignmentAsync(
        IReadOnlyCollection<int> reservationIds,
        CancellationToken cancellationToken)
    {
        if (reservationIds.Count == 0)
        {
            return [];
        }

        var affected = await (
                from rezervasyon in _stysDbContext.Rezervasyonlar
                join segment in _stysDbContext.RezervasyonSegmentleri on rezervasyon.Id equals segment.RezervasyonId
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                join blok in _stysDbContext.OdaKullanimBloklari on atama.OdaId equals blok.OdaId
                where reservationIds.Contains(rezervasyon.Id)
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi
                      && blok.AktifMi
                      && blok.BaslangicTarihi < segment.BitisTarihi
                      && blok.BitisTarihi > segment.BaslangicTarihi
                select rezervasyon.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        return affected.ToHashSet();
    }

    private async Task EnsureNoActiveRoomBlockForReservationAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var blockedAssignment = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                join atama in _stysDbContext.RezervasyonSegmentOdaAtamalari on segment.Id equals atama.RezervasyonSegmentId
                join blok in _stysDbContext.OdaKullanimBloklari on atama.OdaId equals blok.OdaId
                where segment.RezervasyonId == rezervasyonId
                      && blok.AktifMi
                      && blok.BaslangicTarihi < segment.BitisTarihi
                      && blok.BitisTarihi > segment.BaslangicTarihi
                orderby segment.BaslangicTarihi, atama.OdaId
                select new
                {
                    atama.OdaNoSnapshot,
                    blok.BlokTipi,
                    blok.BaslangicTarihi,
                    blok.BitisTarihi
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (blockedAssignment is null)
        {
            return;
        }

        throw new BaseException(
            $"Check-in icin oda degisimi gereklidir. '{blockedAssignment.OdaNoSnapshot}' odasi icin {blockedAssignment.BlokTipi} kaydi mevcut ({blockedAssignment.BaslangicTarihi:dd.MM.yyyy HH:mm} - {blockedAssignment.BitisTarihi:dd.MM.yyyy HH:mm}).",
            400);
    }

    private static List<KonaklamaSenaryoDto> BuildSingleSegmentVariants(
        int kisiSayisi,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        DateTime baslangic,
        DateTime bitis,
        IReadOnlyCollection<RoomAvailability> availabilities)
    {
        var scenarios = new List<KonaklamaSenaryoDto>();
        if (availabilities.Count == 0)
        {
            return scenarios;
        }

        var variantSources = new List<(string Description, List<RoomAvailability> Rooms)>
        {
            ("Tek parca konaklama - minimum oda sayisi", availabilities.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList()),
            ("Tek parca konaklama - az paylasimli tercih", availabilities.OrderBy(x => x.PaylasimliMi).ThenByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList()),
            ("Tek parca konaklama - daginik alternatif", availabilities.OrderBy(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList())
        };

        foreach (var variant in variantSources)
        {
            var allocations = AllocatePeople(variant.Rooms, kisiSayisi, guestGenderRequirements);
            if (allocations.Count == 0)
            {
                continue;
            }

            scenarios.Add(new KonaklamaSenaryoDto
            {
                SenaryoKodu = "SENARYO-X",
                Aciklama = variant.Description,
                ToplamOdaSayisi = allocations.Count,
                OdaDegisimSayisi = 0,
                Segmentler =
                [
                    new KonaklamaSenaryoSegmentDto
                    {
                        BaslangicTarihi = baslangic,
                        BitisTarihi = bitis,
                        OdaAtamalari = allocations
                    }
                ]
            });
        }

        return scenarios;
    }

    private async Task<KonaklamaSenaryoDto?> BuildTwoSegmentScenarioAsync(
        KonaklamaSenaryoAramaRequestDto request,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        CancellationToken cancellationToken)
    {
        var midpoint = request.BaslangicTarihi + TimeSpan.FromTicks((request.BitisTarihi - request.BaslangicTarihi).Ticks / 2);
        if (midpoint <= request.BaslangicTarihi || midpoint >= request.BitisTarihi)
        {
            return null;
        }

        var firstSegmentRooms = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            guestGenderRequirements,
            request.BaslangicTarihi,
            midpoint,
            cancellationToken);
        var secondSegmentRooms = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            guestGenderRequirements,
            midpoint,
            request.BitisTarihi,
            cancellationToken);

        var firstAllocations = AllocatePeople(firstSegmentRooms.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(), request.KisiSayisi, guestGenderRequirements);
        var secondAllocations = AllocatePeople(secondSegmentRooms.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(), request.KisiSayisi, guestGenderRequirements);
        if (firstAllocations.Count == 0 || secondAllocations.Count == 0)
        {
            return null;
        }

        var firstPattern = firstAllocations
            .OrderBy(x => x.OdaId)
            .ThenBy(x => x.AyrilanKisiSayisi)
            .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
            .ToArray();
        var secondPattern = secondAllocations
            .OrderBy(x => x.OdaId)
            .ThenBy(x => x.AyrilanKisiSayisi)
            .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
            .ToArray();

        // Segmentler arasi oda/dağılım aynıysa segmentli senaryo anlamlı değildir.
        if (firstPattern.SequenceEqual(secondPattern))
        {
            return null;
        }

        return new KonaklamaSenaryoDto
        {
            SenaryoKodu = "SENARYO-X",
            Aciklama = "Iki segmentli konaklama (oda degisimi olabilir)",
            ToplamOdaSayisi = firstAllocations.Select(x => x.OdaId).Union(secondAllocations.Select(x => x.OdaId)).Count(),
            OdaDegisimSayisi = 1,
            Segmentler =
            [
                new KonaklamaSenaryoSegmentDto
                {
                    BaslangicTarihi = request.BaslangicTarihi,
                    BitisTarihi = midpoint,
                    OdaAtamalari = firstAllocations
                },
                new KonaklamaSenaryoSegmentDto
                {
                    BaslangicTarihi = midpoint,
                    BitisTarihi = request.BitisTarihi,
                    OdaAtamalari = secondAllocations
                }
            ]
        };
    }

    private static List<KonaklamaSenaryoOdaAtamaDto> AllocatePeople(
        IReadOnlyList<RoomAvailability> rooms,
        int totalPeople,
        ScenarioGuestGenderRequirements guestGenderRequirements)
    {
        if (!guestGenderRequirements.RequiresSharedGenderAwareAllocation)
        {
            return AllocatePeopleWithoutGenderRules(rooms, totalPeople);
        }

        var allocations = new List<KonaklamaSenaryoOdaAtamaDto>();
        return TryAllocatePeopleWithGenderRules(
            rooms,
            0,
            guestGenderRequirements.KadinSayisi,
            guestGenderRequirements.ErkekSayisi,
            allocations)
            ? allocations
            : [];
    }

    private static List<KonaklamaSenaryoOdaAtamaDto> AllocatePeopleWithoutGenderRules(
        IReadOnlyList<RoomAvailability> rooms,
        int totalPeople)
    {
        var remainingPeople = totalPeople;
        var allocations = new List<KonaklamaSenaryoOdaAtamaDto>();

        foreach (var room in rooms)
        {
            if (remainingPeople <= 0)
            {
                break;
            }

            var assign = Math.Min(remainingPeople, room.RemainingCapacity);
            if (assign <= 0)
            {
                continue;
            }

            allocations.Add(new KonaklamaSenaryoOdaAtamaDto
            {
                OdaId = room.OdaId,
                OdaNo = room.OdaNo,
                BinaId = room.BinaId,
                BinaAdi = room.BinaAdi,
                OdaTipiId = room.OdaTipiId,
                OdaTipiAdi = room.OdaTipiAdi,
                Kapasite = room.Kapasite,
                PaylasimliMi = room.PaylasimliMi,
                AyrilanKisiSayisi = assign
            });
            remainingPeople -= assign;
        }

        return remainingPeople == 0 ? allocations : [];
    }

    private static bool TryAllocatePeopleWithGenderRules(
        IReadOnlyList<RoomAvailability> rooms,
        int roomIndex,
        int kalanKadin,
        int kalanErkek,
        List<KonaklamaSenaryoOdaAtamaDto> allocations)
    {
        if (kalanKadin == 0 && kalanErkek == 0)
        {
            return true;
        }

        if (roomIndex >= rooms.Count)
        {
            return false;
        }

        var room = rooms[roomIndex];
        var maxAssignable = Math.Min(room.RemainingCapacity, kalanKadin + kalanErkek);

        foreach (var option in BuildGenderAllocationOptions(room, maxAssignable, kalanKadin, kalanErkek))
        {
            var nextKadin = kalanKadin - option.KadinSayisi;
            var nextErkek = kalanErkek - option.ErkekSayisi;
            if (nextKadin < 0 || nextErkek < 0)
            {
                continue;
            }

            if (option.ToplamKisi > 0)
            {
                allocations.Add(new KonaklamaSenaryoOdaAtamaDto
                {
                    OdaId = room.OdaId,
                    OdaNo = room.OdaNo,
                    BinaId = room.BinaId,
                    BinaAdi = room.BinaAdi,
                    OdaTipiId = room.OdaTipiId,
                    OdaTipiAdi = room.OdaTipiAdi,
                    Kapasite = room.Kapasite,
                    PaylasimliMi = room.PaylasimliMi,
                    AyrilanKisiSayisi = option.ToplamKisi
                });
            }

            if (TryAllocatePeopleWithGenderRules(rooms, roomIndex + 1, nextKadin, nextErkek, allocations))
            {
                return true;
            }

            if (option.ToplamKisi > 0)
            {
                allocations.RemoveAt(allocations.Count - 1);
            }
        }

        return false;
    }

    private static IEnumerable<GenderAllocationOption> BuildGenderAllocationOptions(
        RoomAvailability room,
        int maxAssignable,
        int kalanKadin,
        int kalanErkek)
    {
        yield return new GenderAllocationOption(0, 0);

        if (maxAssignable <= 0)
        {
            yield break;
        }

        if (!room.PaylasimliMi)
        {
            for (var total = maxAssignable; total >= 1; total--)
            {
                var minKadin = Math.Max(0, total - kalanErkek);
                var maxKadin = Math.Min(total, kalanKadin);
                for (var kadin = maxKadin; kadin >= minKadin; kadin--)
                {
                    var erkek = total - kadin;
                    if (erkek <= kalanErkek)
                    {
                        yield return new GenderAllocationOption(kadin, erkek);
                    }
                }
            }

            yield break;
        }

        var fixedGender = room.SharedRoomGender;
        if (string.Equals(fixedGender, KonaklayanCinsiyetleri.Kadin, StringComparison.OrdinalIgnoreCase))
        {
            for (var kadin = Math.Min(maxAssignable, kalanKadin); kadin >= 1; kadin--)
            {
                yield return new GenderAllocationOption(kadin, 0);
            }

            yield break;
        }

        if (string.Equals(fixedGender, KonaklayanCinsiyetleri.Erkek, StringComparison.OrdinalIgnoreCase))
        {
            for (var erkek = Math.Min(maxAssignable, kalanErkek); erkek >= 1; erkek--)
            {
                yield return new GenderAllocationOption(0, erkek);
            }

            yield break;
        }

        for (var kadin = Math.Min(maxAssignable, kalanKadin); kadin >= 1; kadin--)
        {
            yield return new GenderAllocationOption(kadin, 0);
        }

        for (var erkek = Math.Min(maxAssignable, kalanErkek); erkek >= 1; erkek--)
        {
            yield return new GenderAllocationOption(0, erkek);
        }
    }

    private static string CreateScenarioKey(KonaklamaSenaryoDto scenario)
    {
        var segmentKeys = scenario.Segmentler
            .Select(segment =>
            {
                var assignments = segment.OdaAtamalari
                    .OrderBy(x => x.OdaId)
                    .ThenBy(x => x.AyrilanKisiSayisi)
                    .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
                    .ToArray();
                return $"{segment.BaslangicTarihi:O}-{segment.BitisTarihi:O}-[{string.Join(",", assignments)}]";
            });

        return string.Join("|", segmentKeys);
    }

    private static int CalculateOverlapNights(DateTime girisTarihi, DateTime cikisTarihi, DateTime aralikBaslangic, DateTime aralikBitisExclusive)
    {
        var overlapStart = girisTarihi > aralikBaslangic ? girisTarihi : aralikBaslangic;
        var overlapEnd = cikisTarihi < aralikBitisExclusive ? cikisTarihi : aralikBitisExclusive;

        if (overlapEnd <= overlapStart)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Ceiling((overlapEnd - overlapStart).TotalDays));
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureTesisHasKonaklamaTipiAsync(int tesisId, int konaklamaTipiId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.TesisKonaklamaTipleri.AnyAsync(x =>
            x.TesisId == tesisId
            && x.KonaklamaTipiId == konaklamaTipiId
            && x.AktifMi
            && !x.IsDeleted
            && x.KonaklamaTipi != null
            && x.KonaklamaTipi.AktifMi,
            cancellationToken);

        if (!exists)
        {
            throw new BaseException("Secilen konaklama tipi bu tesiste kullanima acik degil.", 400);
        }
    }

    private async Task EnsureTesisHasMisafirTipiAsync(int tesisId, int misafirTipiId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.TesisMisafirTipleri.AnyAsync(x =>
            x.TesisId == tesisId
            && x.MisafirTipiId == misafirTipiId
            && x.AktifMi
            && !x.IsDeleted
            && x.MisafirTipi != null
            && x.MisafirTipi.AktifMi,
            cancellationToken);

        if (!exists)
        {
            throw new BaseException("Secilen misafir tipi bu tesiste kullanima acik degil.", 400);
        }
    }

    private async Task<List<RezervasyonKonaklamaHakki>> BuildKonaklamaHaklariAsync(
        int tesisId,
        int konaklamaTipiId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        CancellationToken cancellationToken)
    {
        var icerikKalemleri = (await GetEffectiveKonaklamaTipiIcerikMapAsync(tesisId, [konaklamaTipiId], cancellationToken))
            .GetValueOrDefault(konaklamaTipiId, []);

        if (icerikKalemleri.Count == 0)
        {
            return [];
        }

        var girisGun = girisTarihi.Date;
        var cikisGun = cikisTarihi.Date;
        var haklar = new List<RezervasyonKonaklamaHakki>();

        foreach (var item in icerikKalemleri)
        {
            if (string.Equals(item.Periyot, KonaklamaTipiIcerikPeriyotlari.Gunluk, StringComparison.OrdinalIgnoreCase))
            {
                for (var tarih = girisGun; tarih < cikisGun; tarih = tarih.AddDays(1))
                {
                    if ((tarih == girisGun && !item.CheckInGunuGecerliMi)
                        || (tarih == cikisGun.AddDays(-1) && !item.CheckOutGunuGecerliMi))
                    {
                        continue;
                    }

                    haklar.Add(new RezervasyonKonaklamaHakki
                    {
                        HizmetKodu = item.HizmetKodu,
                        HizmetAdiSnapshot = item.HizmetAdi,
                        Miktar = item.Miktar,
                        Periyot = item.Periyot,
                        PeriyotAdiSnapshot = item.PeriyotAdi,
                        KullanimTipi = item.KullanimTipi,
                        KullanimTipiAdiSnapshot = item.KullanimTipiAdi,
                        KullanimNoktasi = item.KullanimNoktasi,
                        KullanimNoktasiAdiSnapshot = item.KullanimNoktasiAdi,
                        KullanimBaslangicSaati = ParseTimeOrNull(item.KullanimBaslangicSaati),
                        KullanimBitisSaati = ParseTimeOrNull(item.KullanimBitisSaati),
                        CheckInGunuGecerliMi = item.CheckInGunuGecerliMi,
                        CheckOutGunuGecerliMi = item.CheckOutGunuGecerliMi,
                        HakTarihi = tarih,
                        AciklamaSnapshot = item.Aciklama,
                        Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor,
                        AktifMi = true
                    });
                }

                continue;
            }

            haklar.Add(new RezervasyonKonaklamaHakki
            {
                HizmetKodu = item.HizmetKodu,
                HizmetAdiSnapshot = item.HizmetAdi,
                Miktar = item.Miktar,
                Periyot = item.Periyot,
                PeriyotAdiSnapshot = item.PeriyotAdi,
                KullanimTipi = item.KullanimTipi,
                KullanimTipiAdiSnapshot = item.KullanimTipiAdi,
                KullanimNoktasi = item.KullanimNoktasi,
                KullanimNoktasiAdiSnapshot = item.KullanimNoktasiAdi,
                KullanimBaslangicSaati = ParseTimeOrNull(item.KullanimBaslangicSaati),
                KullanimBitisSaati = ParseTimeOrNull(item.KullanimBitisSaati),
                CheckInGunuGecerliMi = item.CheckInGunuGecerliMi,
                CheckOutGunuGecerliMi = item.CheckOutGunuGecerliMi,
                HakTarihi = girisGun,
                AciklamaSnapshot = item.Aciklama,
                Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor,
                AktifMi = true
            });
        }

        return haklar;
    }

    private void ValidateReservationAllowsHakTuketim(Rezervasyon reservation)
    {
        if (reservation.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("Iptal edilen rezervasyonda konaklama hakki tuketimi yapilamaz.", 400);
        }

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Konaklama hakki tuketimi yalnizca check-in tamamlanan rezervasyonda yapilabilir.", 400);
        }
    }

    private void ValidateHakTuketimRequest(
        Rezervasyon reservation,
        RezervasyonKonaklamaHakki hak,
        DateTime tuketimTarihi,
        int miktar)
    {
        if (miktar <= 0)
        {
            throw new BaseException("Tuketim miktari sifirdan buyuk olmalidir.", 400);
        }

        var tuketimGunu = tuketimTarihi.Date;
        var girisGunu = reservation.GirisTarihi.Date;
        var cikisGunu = reservation.CikisTarihi.Date;

        if (tuketimGunu < girisGunu || tuketimGunu > cikisGunu)
        {
            throw new BaseException("Tuketim tarihi rezervasyon araliginda olmalidir.", 400);
        }

        if (hak.Periyot == KonaklamaTipiIcerikPeriyotlari.Gunluk
            && hak.HakTarihi.HasValue
            && tuketimGunu != hak.HakTarihi.Value.Date)
        {
            throw new BaseException("Gunluk hak yalnizca kendi gununde tuketilebilir.", 400);
        }

        if (tuketimGunu == girisGunu && !hak.CheckInGunuGecerliMi)
        {
            throw new BaseException("Bu hak check-in gununde kullanilamaz.", 400);
        }

        if (tuketimGunu == cikisGunu && !hak.CheckOutGunuGecerliMi)
        {
            throw new BaseException("Bu hak check-out gununde kullanilamaz.", 400);
        }

        if (hak.KullanimBaslangicSaati.HasValue && hak.KullanimBitisSaati.HasValue)
        {
            var saat = tuketimTarihi.TimeOfDay;
            if (saat < hak.KullanimBaslangicSaati.Value || saat > hak.KullanimBitisSaati.Value)
            {
                throw new BaseException(
                    $"Bu hak yalnizca {FormatTime(hak.KullanimBaslangicSaati)} - {FormatTime(hak.KullanimBitisSaati)} saatleri arasinda kullanilabilir.",
                    400);
            }
        }
    }

    private async Task<int> GetToplamHakTuketimiAsync(int hakId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.RezervasyonKonaklamaHakkiTuketimKayitlari
            .Where(x => x.RezervasyonKonaklamaHakkiId == hakId && x.AktifMi && !x.IsDeleted)
            .SumAsync(x => (int?)x.Miktar, cancellationToken) ?? 0;
    }

    private async Task<List<RezervasyonKonaklamaHakkiTuketimNoktasiDto>> GetTuketimNoktalariAsync(int tesisId, CancellationToken cancellationToken)
    {
        return await _stysDbContext.IsletmeAlanlari
            .Where(x => x.AktifMi
                && !x.IsDeleted
                && x.Bina != null
                && x.Bina.TesisId == tesisId
                && x.Bina.AktifMi
                && x.IsletmeAlaniSinifi != null
                && x.IsletmeAlaniSinifi.AktifMi)
            .OrderBy(x => x.IsletmeAlaniSinifi!.Ad)
            .ThenBy(x => x.OzelAd ?? x.IsletmeAlaniSinifi!.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonKonaklamaHakkiTuketimNoktasiDto
            {
                Id = x.Id,
                Ad = x.OzelAd ?? x.IsletmeAlaniSinifi!.Ad,
                BinaAdi = x.Bina!.Ad,
                SinifKod = x.IsletmeAlaniSinifi!.Kod,
                SinifAd = x.IsletmeAlaniSinifi!.Ad
            })
            .ToListAsync(cancellationToken);
    }

    private static IEnumerable<RezervasyonKonaklamaHakkiTuketimNoktasiDto> GetApplicableTuketimNoktalari(
        IReadOnlyCollection<RezervasyonKonaklamaHakkiTuketimNoktasiDto> allPoints,
        string kullanimNoktasi)
    {
        var sinifKodlari = GetExpectedIsletmeAlaniSinifKodlari(kullanimNoktasi);
        if (sinifKodlari.Count == 0)
        {
            return [];
        }

        return allPoints.Where(x => sinifKodlari.Contains(x.SinifKod, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<(int Id, string Ad)?> ResolveTuketimNoktasiAsync(
        int tesisId,
        string kullanimNoktasi,
        int? isletmeAlaniId,
        CancellationToken cancellationToken)
    {
        var sinifKodlari = GetExpectedIsletmeAlaniSinifKodlari(kullanimNoktasi);
        if (sinifKodlari.Count == 0)
        {
            return null;
        }

        if (!isletmeAlaniId.HasValue || isletmeAlaniId.Value <= 0)
        {
            throw new BaseException("Bu hak icin bir tuketim noktasi secilmelidir.", 400);
        }

        var alan = await _stysDbContext.IsletmeAlanlari
            .Where(x => x.Id == isletmeAlaniId.Value
                && x.AktifMi
                && !x.IsDeleted
                && x.Bina != null
                && x.Bina.TesisId == tesisId
                && x.Bina.AktifMi
                && x.IsletmeAlaniSinifi != null
                && x.IsletmeAlaniSinifi.AktifMi
                && sinifKodlari.Contains(x.IsletmeAlaniSinifi.Kod))
            .Select(x => new
            {
                x.Id,
                Ad = x.OzelAd ?? x.IsletmeAlaniSinifi!.Ad
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (alan is null)
        {
            throw new BaseException("Secilen tuketim noktasi hak ile uyumlu degil.", 400);
        }

        return (alan.Id, alan.Ad);
    }

    private static IReadOnlyCollection<string> GetExpectedIsletmeAlaniSinifKodlari(string kullanimNoktasi)
    {
        return kullanimNoktasi switch
        {
            KonaklamaTipiIcerikKullanimNoktalari.Restoran => ["RESTORAN"],
            KonaklamaTipiIcerikKullanimNoktalari.Bar => ["BAR"],
            KonaklamaTipiIcerikKullanimNoktalari.OdaServisi => ["ODA_SERVISI", "ODA SERVISI", "ODA-SERVISI"],
            _ => []
        };
    }

    private static string CalculateHakDurumu(RezervasyonKonaklamaHakki hak, int tuketilenMiktar)
    {
        if (hak.KullanimTipi == KonaklamaTipiIcerikKullanimTipleri.Sinirsiz)
        {
            return tuketilenMiktar > 0
                ? RezervasyonKonaklamaHakDurumlari.Kullanildi
                : RezervasyonKonaklamaHakDurumlari.Bekliyor;
        }

        return tuketilenMiktar >= hak.Miktar
            ? RezervasyonKonaklamaHakDurumlari.Kullanildi
            : RezervasyonKonaklamaHakDurumlari.Bekliyor;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > maxLength
            ? normalized[..maxLength]
            : normalized;
    }

    private async Task<Rezervasyon> GetScopedReservationForManageAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        var reservation = await _stysDbContext.Rezervasyonlar
            .FirstOrDefaultAsync(x => x.Id == rezervasyonId, cancellationToken);

        if (reservation is null)
        {
            throw new BaseException("Rezervasyon bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(reservation.TesisId, cancellationToken);
        return reservation;
    }

    private static RezervasyonKayitSonucDto ToSaveResult(Rezervasyon reservation)
    {
        return new RezervasyonKayitSonucDto
        {
            Id = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            RezervasyonDurumu = reservation.RezervasyonDurumu
        };
    }

    private async Task<Dictionary<int, List<RezervasyonKonaklamaTipiIcerikDto>>> GetEffectiveKonaklamaTipiIcerikMapAsync(
        int tesisId,
        IReadOnlyCollection<int> konaklamaTipiIds,
        CancellationToken cancellationToken)
    {
        if (konaklamaTipiIds.Count == 0)
        {
            return [];
        }

        var globalItems = await _stysDbContext.KonaklamaTipiIcerikKalemleri
            .Where(x => konaklamaTipiIds.Contains(x.KonaklamaTipiId) && !x.IsDeleted)
            .OrderBy(x => x.HizmetKodu)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.KonaklamaTipiId,
                x.HizmetKodu,
                x.Miktar,
                x.Periyot,
                x.KullanimTipi,
                x.KullanimNoktasi,
                x.KullanimBaslangicSaati,
                x.KullanimBitisSaati,
                x.CheckInGunuGecerliMi,
                x.CheckOutGunuGecerliMi,
                x.Aciklama
            })
            .ToListAsync(cancellationToken);

        if (globalItems.Count == 0)
        {
            return [];
        }

        var itemIds = globalItems.Select(x => x.Id).ToList();
        var overrides = await _stysDbContext.Set<TesisKonaklamaTipiIcerikOverride>()
            .Where(x => x.TesisId == tesisId
                && itemIds.Contains(x.KonaklamaTipiIcerikKalemiId)
                && !x.IsDeleted)
            .ToDictionaryAsync(x => x.KonaklamaTipiIcerikKalemiId, cancellationToken);

        return globalItems
            .Select(item =>
            {
                overrides.TryGetValue(item.Id, out var overrideItem);
                if (overrideItem?.DevreDisiMi == true)
                {
                    return null;
                }

                var periyot = overrideItem?.Periyot ?? item.Periyot;
                var kullanimTipi = overrideItem?.KullanimTipi ?? item.KullanimTipi;
                var kullanimNoktasi = overrideItem?.KullanimNoktasi ?? item.KullanimNoktasi;

                return new
                {
                    item.KonaklamaTipiId,
                    Icerik = new RezervasyonKonaklamaTipiIcerikDto
                    {
                        HizmetKodu = item.HizmetKodu,
                        HizmetAdi = KonaklamaTipiIcerikHizmetKodlari.GetAd(item.HizmetKodu),
                        Miktar = overrideItem?.Miktar ?? item.Miktar,
                        Periyot = periyot,
                        PeriyotAdi = KonaklamaTipiIcerikPeriyotlari.GetAd(periyot),
                        KullanimTipi = kullanimTipi,
                        KullanimTipiAdi = KonaklamaTipiIcerikKullanimTipleri.GetAd(kullanimTipi),
                        KullanimNoktasi = kullanimNoktasi,
                        KullanimNoktasiAdi = KonaklamaTipiIcerikKullanimNoktalari.GetAd(kullanimNoktasi),
                        KullanimBaslangicSaati = FormatTime(overrideItem?.KullanimBaslangicSaati ?? item.KullanimBaslangicSaati),
                        KullanimBitisSaati = FormatTime(overrideItem?.KullanimBitisSaati ?? item.KullanimBitisSaati),
                        CheckInGunuGecerliMi = overrideItem?.CheckInGunuGecerliMi ?? item.CheckInGunuGecerliMi,
                        CheckOutGunuGecerliMi = overrideItem?.CheckOutGunuGecerliMi ?? item.CheckOutGunuGecerliMi,
                        Aciklama = overrideItem?.Aciklama ?? item.Aciklama
                    }
                };
            })
            .Where(x => x is not null)
            .GroupBy(x => x!.KonaklamaTipiId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x!.Icerik).ToList());
    }

    private static TimeSpan? ParseTimeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : TimeSpan.ParseExact(value, @"hh\:mm", null);

    private static string? FormatTime(TimeSpan? value)
        => value.HasValue ? value.Value.ToString(@"hh\:mm") : null;

    private sealed record RoomAvailability(
        int OdaId,
        string OdaNo,
        int BinaId,
        string BinaAdi,
        int OdaTipiId,
        string OdaTipiAdi,
        int Kapasite,
        bool PaylasimliMi,
        int RemainingCapacity,
        string? SharedRoomGender);

    private sealed record RoomInfo(
        int OdaId,
        string OdaNo,
        int TesisId,
        string BinaAdi,
        string OdaTipiAdi,
        int Kapasite,
        bool PaylasimliMi);

    private sealed record OdaDegisimRoomInfo(
        int OdaId,
        string OdaNo,
        string BinaAdi,
        string OdaTipiAdi,
        int Kapasite,
        bool PaylasimliMi);

    private sealed record OdaDegisimAssignmentInfo(
        int RezervasyonSegmentOdaAtamaId,
        int SegmentId,
        int SegmentSirasi,
        DateTime BaslangicTarihi,
        DateTime BitisTarihi,
        int AyrilanKisiSayisi,
        int OdaId,
        string OdaNo,
        string BinaAdi,
        string OdaTipiAdi,
        bool PaylasimliMi,
        int Kapasite);

    private sealed record OdaDegisimAppliedChange(
        int RezervasyonSegmentOdaAtamaId,
        int SegmentId,
        int SegmentSirasi,
        DateTime BaslangicTarihi,
        DateTime BitisTarihi,
        int EskiOdaId,
        int YeniOdaId);

    private sealed record SharedRoomGuestSelection(
        int GuestSiraNo,
        string? Cinsiyet,
        int SegmentId,
        int OdaId);

    private sealed record SharedRoomGuestOccupancy(
        int OdaId,
        DateTime BaslangicTarihi,
        DateTime BitisTarihi,
        string? Cinsiyet);

    private sealed record RoomTypePricingInfo(int OdaTipiId, bool PaylasimliMi);

    private sealed record SelectedScenarioPrice(string ParaBirimi, decimal Tutar);

    private sealed record ScenarioGuestGenderRequirements(
        int KadinSayisi,
        int ErkekSayisi,
        int ToplamKisiSayisi)
    {
        public bool HasKnownGenders => KadinSayisi > 0 || ErkekSayisi > 0;

        public bool HasMixedKnownGenders => KadinSayisi > 0 && ErkekSayisi > 0;

        public bool RequiresSharedGenderAwareAllocation => HasMixedKnownGenders;

        public int MaxSameGenderGroupSize => Math.Max(KadinSayisi, ErkekSayisi);

        public bool CanUseSharedRoomOfGender(string cinsiyet)
        {
            if (!HasKnownGenders)
            {
                return true;
            }

            if (string.Equals(cinsiyet, KonaklayanCinsiyetleri.Kadin, StringComparison.OrdinalIgnoreCase))
            {
                return KadinSayisi > 0;
            }

            if (string.Equals(cinsiyet, KonaklayanCinsiyetleri.Erkek, StringComparison.OrdinalIgnoreCase))
            {
                return ErkekSayisi > 0;
            }

            return false;
        }

        public static ScenarioGuestGenderRequirements None(int toplamKisiSayisi) => new(0, 0, toplamKisiSayisi);
    }

    private sealed record GenderAllocationOption(int KadinSayisi, int ErkekSayisi)
    {
        public int ToplamKisi => KadinSayisi + ErkekSayisi;
    }
}
