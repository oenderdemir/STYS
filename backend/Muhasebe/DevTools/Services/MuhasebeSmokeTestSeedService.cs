using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kullanicilar.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.DevTools.Services;
using STYS.Muhasebe.Hesaplar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Identity.Infrastructure.EntityFramework;

namespace STYS.Muhasebe.DevTools.Services;

public sealed class MuhasebeSmokeTestSeedService : IMuhasebeSmokeTestSeedService
{
    private const string SeedUserName = "muhasebe-admin";
    private const string TestTesisName = "TEST MUHASEBE TESISI";
    private const string ForbiddenTesisName = "TEST MUHASEBE YETKISIZ TESISI";
    private const string TestCariMusteriKodu = "TEST-CARI-MUSTERI";
    private const string TestCariTedarikciKodu = "TEST-CARI-TEDARIKCI";
    private const string TestKasaKodu = "TEST-KASA-001";
    private const string TestBankaKodu = "TEST-BANKA-001";
    private const string TestFisNo = "TEST-FIS-0001";
    private const string TestCariHareketBelgeNo = "TEST-CARI-HAREKET-0001";

    private readonly StysAppDbContext _db;
    private readonly TodIdentityDbContext _identityDb;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MuhasebeSmokeTestSeedService> _logger;

    public MuhasebeSmokeTestSeedService(
        StysAppDbContext db,
        TodIdentityDbContext identityDb,
        IHostEnvironment environment,
        ILogger<MuhasebeSmokeTestSeedService> logger)
    {
        _db = db;
        _identityDb = identityDb;
        _environment = environment;
        _logger = logger;
    }

    public async Task<MuhasebeSmokeTestSeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!(_environment.IsDevelopment() || _environment.IsEnvironment("Test")))
        {
            throw new InvalidOperationException("Muhasebe smoke test seed servisi sadece development/test ortamında çalıştırılabilir.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonth = now.AddMonths(-1);
        var previousMonthStart = new DateTime(previousMonth.Year, previousMonth.Month, 1);
        var previousMonthEnd = previousMonthStart.AddMonths(1).AddDays(-1);

        var seedUser = await _identityDb.Users
            .FirstOrDefaultAsync(x =>
                x.UserName == SeedUserName ||
                x.UserName == "admin",
                cancellationToken);

        if (seedUser is null)
        {
            throw new InvalidOperationException("Seed kullanıcısı bulunamadı. 'muhasebe-admin' veya 'admin' hesabı gereklidir.");
        }

        var il = await _db.Set<Il>()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Iller tablosunda seed için kullanılacak kayıt bulunamadı.");

        var testTesis = await EnsureTesisAsync(TestTesisName, il.Id, cancellationToken);
        var forbiddenTesis = await EnsureTesisAsync(ForbiddenTesisName, il.Id, cancellationToken);
        await EnsureTesisMuhasebeciAsync(testTesis.Id, seedUser.Id, cancellationToken);
        await EnsureKullaniciTesisSahiplikAsync(testTesis.Id, seedUser.Id, cancellationToken);

        var openDonem = await EnsureDonemAsync(
            testTesis.Id,
            currentMonthStart.Year,
            currentMonthStart.Month,
            currentMonthStart,
            currentMonthStart.AddMonths(1).AddDays(-1),
            kapaliMi: false,
            cancellationToken);

        var closedDonem = await EnsureDonemAsync(
            testTesis.Id,
            previousMonthStart.Year,
            previousMonthStart.Month,
            previousMonthStart,
            previousMonthEnd,
            kapaliMi: true,
            cancellationToken);

        var hesaplar = await EnsureHesapPlaniSetAsync(testTesis.Id, cancellationToken);
        var cariMusteri = await EnsureCariKartAsync(
            testTesis.Id,
            hesaplar.CustomerAccount.Id,
            TestCariMusteriKodu,
            CariKartTipleri.Musteri,
            "TEST CARI MUSTERI",
            cancellationToken);
        await EnsureCariKartYetkiliKisiAsync(cariMusteri.Id, "TEST YETKILI KISI 1", cancellationToken);

        var cariTedarikci = await EnsureCariKartAsync(
            testTesis.Id,
            hesaplar.SupplierAccount.Id,
            TestCariTedarikciKodu,
            CariKartTipleri.Tedarikci,
            "TEST CARI TEDARIKCI",
            cancellationToken);
        await EnsureCariKartYetkiliKisiAsync(cariTedarikci.Id, "TEST YETKILI KISI 2", cancellationToken);

        await EnsureKasaBankaHesapAsync(
            testTesis.Id,
            hesaplar.CashAccount.Id,
            KasaBankaHesapTipleri.NakitKasa,
            TestKasaKodu,
            "TEST MUHASEBE KASA",
            cancellationToken);

        await EnsureKasaBankaHesapAsync(
            testTesis.Id,
            hesaplar.BankAccount.Id,
            KasaBankaHesapTipleri.Banka,
            TestBankaKodu,
            "TEST MUHASEBE BANKA",
            cancellationToken);

        var cariHareket = await EnsureCariHareketAsync(
            cariMusteri.Id,
            now,
            cancellationToken);

        var fis = await EnsureMuhasebeFisAsync(
            testTesis.Id,
            openDonem,
            now,
            hesaplar,
            cariMusteri.Id,
            cancellationToken);

        await EnsureYevmiyeNoSayaciAsync(testTesis.Id, openDonem.MaliYil, 1, cancellationToken);
        await EnsureMuhasebeHesapBakiyeleriAsync(testTesis.Id, openDonem, hesaplar, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Muhasebe smoke test seed tamamlandı. Tesis={Tesis}, ForbiddenTesis={ForbiddenTesis}, FisNo={FisNo}, CariHareket={CariHareket}.",
            testTesis.Ad, forbiddenTesis.Ad, fis.FisNo, cariHareket.BelgeNo);

        return new MuhasebeSmokeTestSeedResultDto
        {
            EnvironmentName = _environment.EnvironmentName,
            TestUserName = seedUser.UserName ?? SeedUserName,
            TestTesisName = testTesis.Ad,
            ForbiddenTesisName = forbiddenTesis.Ad,
            SeededAt = DateTimeOffset.Now,
            Notes =
            [
                "Test tesisi ve yetkisiz tesis oluşturuldu veya yeniden kullanıldı.",
                "Açık ve kapalı muhasebe dönemleri hazırlandı.",
                "TEST_ önekli cari kartlar, banka/kasa hesapları ve muhasebe hesap planı hazırlandı.",
                "Açık cari hareket, onaylı muhasebe fişi ve yevmiye sayaç/bakiye satırları hazırlandı.",
                "Satış belgesi ve tahsilat/ödeme smoke testleri için ön koşullar hazırlandı; son belge adımları UI smoke akışında tamamlanacak."
            ]
        };
    }

    private async Task<Tesis> EnsureTesisAsync(string ad, int ilId, CancellationToken cancellationToken)
    {
        var tesis = await _db.Tesisler.FirstOrDefaultAsync(x => x.Ad == ad, cancellationToken);
        if (tesis is not null)
        {
            return tesis;
        }

        tesis = new Tesis
        {
            Ad = ad,
            IlId = ilId,
            Telefon = "TEST-0000",
            Adres = "TEST MUHASEBE ADRESI",
            Eposta = "test-muhasebe@local",
            EkHizmetPaketCakismaPolitikasi = "Yoksay"
        };

        _db.Tesisler.Add(tesis);
        await _db.SaveChangesAsync(cancellationToken);
        return tesis;
    }

    private async Task EnsureTesisMuhasebeciAsync(object tesisId, object userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Set<TesisMuhasebeci>().AnyAsync(
            x => EF.Property<object>(x, "TesisId")!.Equals(tesisId) &&
                 EF.Property<object>(x, "UserId")!.Equals(userId),
            cancellationToken);
        if (!exists)
        {
            var tesisMuhasebeci = new TesisMuhasebeci();
            _db.Set<TesisMuhasebeci>().Add(tesisMuhasebeci);
            _db.Entry(tesisMuhasebeci).Property("TesisId").CurrentValue = tesisId;
            _db.Entry(tesisMuhasebeci).Property("UserId").CurrentValue = userId;
        }
    }

    private async Task EnsureKullaniciTesisSahiplikAsync(object tesisId, object userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Set<KullaniciTesisSahiplik>().AnyAsync(
            x => EF.Property<object>(x, "UserId")!.Equals(userId),
            cancellationToken);
        if (!exists)
        {
            var kullaniciTesisSahiplik = new KullaniciTesisSahiplik();
            _db.Set<KullaniciTesisSahiplik>().Add(kullaniciTesisSahiplik);
            _db.Entry(kullaniciTesisSahiplik).Property("TesisId").CurrentValue = tesisId;
            _db.Entry(kullaniciTesisSahiplik).Property("UserId").CurrentValue = userId;
        }
    }

    private async Task<MuhasebeDonem> EnsureDonemAsync(
        int tesisId,
        int maliYil,
        int donemNo,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        bool kapaliMi,
        CancellationToken cancellationToken)
    {
        var donem = await _db.Set<MuhasebeDonem>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.MaliYil == maliYil && x.DonemNo == donemNo, cancellationToken);
        if (donem is not null)
        {
            return donem;
        }

        donem = new MuhasebeDonem
        {
            TesisId = tesisId,
            MaliYil = maliYil,
            DonemNo = donemNo,
            BaslangicTarihi = baslangicTarihi,
            BitisTarihi = bitisTarihi,
            KapaliMi = kapaliMi,
            KapanisTarihi = kapaliMi ? bitisTarihi : null,
            Aciklama = kapaliMi ? "TEST kapalı dönem" : "TEST açık dönem"
        };

        _db.Set<MuhasebeDonem>().Add(donem);
        await _db.SaveChangesAsync(cancellationToken);
        return donem;
    }

    private async Task<HesapPlaniSet> EnsureHesapPlaniSetAsync(int tesisId, CancellationToken cancellationToken)
    {
        var cashAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-100-KASA", "TEST MUHASEBE KASA HESABI", cancellationToken);
        var bankAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-102-BANKA", "TEST MUHASEBE BANKA HESABI", cancellationToken);
        var customerAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-120-CARI-MUSTERI", "TEST MUHASEBE CARI MUSTERI HESABI", cancellationToken);
        var supplierAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-320-CARI-TEDARIKCI", "TEST MUHASEBE CARI TEDARIKCI HESABI", cancellationToken);
        var revenueAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-600-GELIR", "TEST MUHASEBE GELIR HESABI", cancellationToken);
        var vatAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-191-KDV", "TEST MUHASEBE KDV HESABI", cancellationToken);
        var discountAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-780-INDIRIM", "TEST MUHASEBE INDIRIM HESABI", cancellationToken);
        var stockAccount = await EnsureMuhasebeHesapPlaniAsync(tesisId, "TEST-153-STOK", "TEST MUHASEBE STOK HESABI", cancellationToken);

        return new HesapPlaniSet(
            cashAccount,
            bankAccount,
            customerAccount,
            supplierAccount,
            revenueAccount,
            vatAccount,
            discountAccount,
            stockAccount);
    }

    private async Task<MuhasebeHesapPlani> EnsureMuhasebeHesapPlaniAsync(
        int tesisId,
        string kod,
        string ad,
        CancellationToken cancellationToken)
    {
        var hesap = await _db.Set<MuhasebeHesapPlani>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.Kod == kod, cancellationToken);
        if (hesap is not null)
        {
            return hesap;
        }

        hesap = new MuhasebeHesapPlani
        {
            TesisId = tesisId,
            Kod = kod,
            TamKod = kod,
            Ad = ad,
            SeviyeNo = 3,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true
        };

        _db.Set<MuhasebeHesapPlani>().Add(hesap);
        await _db.SaveChangesAsync(cancellationToken);
        return hesap;
    }

    private async Task<CariKart> EnsureCariKartAsync(
        int tesisId,
        int muhasebeHesapPlaniId,
        string cariKodu,
        string cariTipi,
        string unvan,
        CancellationToken cancellationToken)
    {
        var cariKart = await _db.Set<CariKart>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.CariKodu == cariKodu, cancellationToken);
        if (cariKart is not null)
        {
            return cariKart;
        }

        cariKart = new CariKart
        {
            TesisId = tesisId,
            CariKodu = cariKodu,
            CariTipi = cariTipi,
            UnvanAdSoyad = unvan,
            MuhasebeHesapPlaniId = muhasebeHesapPlaniId
        };

        _db.Set<CariKart>().Add(cariKart);
        await _db.SaveChangesAsync(cancellationToken);
        return cariKart;
    }

    private async Task EnsureCariKartYetkiliKisiAsync(int cariKartId, string adSoyad, CancellationToken cancellationToken)
    {
        var exists = await _db.Set<CariKartYetkiliKisi>().AnyAsync(x => x.CariKartId == cariKartId && x.AdSoyad == adSoyad, cancellationToken);
        if (!exists)
        {
            _db.Set<CariKartYetkiliKisi>().Add(new CariKartYetkiliKisi
            {
                CariKartId = cariKartId,
                AdSoyad = adSoyad
            });
        }
    }

    private async Task<KasaBankaHesap> EnsureKasaBankaHesapAsync(
        int tesisId,
        int muhasebeHesapPlaniId,
        string tip,
        string kod,
        string ad,
        CancellationToken cancellationToken)
    {
        var hesap = await _db.Set<KasaBankaHesap>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.Kod == kod, cancellationToken);
        if (hesap is not null)
        {
            return hesap;
        }

        hesap = new KasaBankaHesap
        {
            TesisId = tesisId,
            MuhasebeHesapPlaniId = muhasebeHesapPlaniId,
            Tip = tip,
            Kod = kod,
            Ad = ad,
            ParaBirimi = "TRY",
            ValorGunSayisi = 0,
            AktifMi = true
        };

        _db.Set<KasaBankaHesap>().Add(hesap);
        await _db.SaveChangesAsync(cancellationToken);
        return hesap;
    }

    private async Task<CariHareket> EnsureCariHareketAsync(
        int cariKartId,
        DateTime hareketTarihi,
        CancellationToken cancellationToken)
    {
        var hareket = await _db.Set<CariHareket>().FirstOrDefaultAsync(x => x.CariKartId == cariKartId && x.BelgeNo == TestCariHareketBelgeNo, cancellationToken);
        if (hareket is not null)
        {
            return hareket;
        }

        hareket = new CariHareket
        {
            CariKartId = cariKartId,
            HareketTarihi = hareketTarihi,
            BelgeTuru = "TEST_SMOKE",
            BelgeNo = TestCariHareketBelgeNo,
            Aciklama = "TEST smoke seed cari hareketi",
            BorcTutari = 1000m,
            AlacakTutari = 0m,
            KapananTutar = 0m,
            KalanTutar = 1000m,
            ParaBirimi = "TRY",
            VadeTarihi = hareketTarihi.AddDays(30),
            Durum = "Acik",
            KaynakModul = MuhasebeKaynakModulleri.Manuel,
            KapandiMi = false
        };

        _db.Set<CariHareket>().Add(hareket);
        await _db.SaveChangesAsync(cancellationToken);
        return hareket;
    }

    private async Task<MuhasebeFis> EnsureMuhasebeFisAsync(
        int tesisId,
        MuhasebeDonem donem,
        DateTime fisTarihi,
        HesapPlaniSet hesaplar,
        int cariKartId,
        CancellationToken cancellationToken)
    {
        var fis = await _db.Set<MuhasebeFis>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.FisNo == TestFisNo, cancellationToken);
        if (fis is not null)
        {
            return fis;
        }

        fis = new MuhasebeFis
        {
            TesisId = tesisId,
            MaliYil = donem.MaliYil,
            Donem = donem.DonemNo,
            FisNo = TestFisNo,
            YevmiyeNo = 1,
            FisTarihi = fisTarihi,
            FisTipi = MuhasebeFisTipleri.Acilis,
            KaynakModul = MuhasebeKaynakModulleri.Manuel,
            Durum = MuhasebeFisDurumlari.Onayli,
            ToplamBorc = 1000m,
            ToplamAlacak = 1000m,
            Aciklama = "TEST smoke seed muhasebe fişi",
            Satirlar =
            [
                new MuhasebeFisSatir
                {
                    SiraNo = 1,
                    MuhasebeHesapPlaniId = hesaplar.CashAccount.Id,
                    Borc = 1000m,
                    Alacak = 0m,
                    ParaBirimi = "TRY",
                    Kur = 1m,
                    CariKartId = cariKartId,
                    Aciklama = "TEST kasa karşılığı"
                },
                new MuhasebeFisSatir
                {
                    SiraNo = 2,
                    MuhasebeHesapPlaniId = hesaplar.RevenueAccount.Id,
                    Borc = 0m,
                    Alacak = 800m,
                    ParaBirimi = "TRY",
                    Kur = 1m,
                    Aciklama = "TEST gelir"
                },
                new MuhasebeFisSatir
                {
                    SiraNo = 3,
                    MuhasebeHesapPlaniId = hesaplar.VatAccount.Id,
                    Borc = 0m,
                    Alacak = 200m,
                    ParaBirimi = "TRY",
                    Kur = 1m,
                    Aciklama = "TEST KDV"
                }
            ]
        };

        _db.Set<MuhasebeFis>().Add(fis);
        await _db.SaveChangesAsync(cancellationToken);
        return fis;
    }

    private async Task EnsureYevmiyeNoSayaciAsync(int tesisId, int maliYil, int sonNumara, CancellationToken cancellationToken)
    {
        var sayac = await _db.Set<MuhasebeYevmiyeNoSayac>().FirstOrDefaultAsync(x => x.TesisId == tesisId && x.MaliYil == maliYil, cancellationToken);
        if (sayac is null)
        {
            _db.Set<MuhasebeYevmiyeNoSayac>().Add(new MuhasebeYevmiyeNoSayac
            {
                TesisId = tesisId,
                MaliYil = maliYil,
                SonNumara = sonNumara
            });
            return;
        }

        if (sayac.SonNumara < sonNumara)
        {
            sayac.SonNumara = sonNumara;
        }
    }

    private async Task EnsureMuhasebeHesapBakiyeleriAsync(
        int tesisId,
        MuhasebeDonem donem,
        HesapPlaniSet hesaplar,
        CancellationToken cancellationToken)
    {
        await EnsureMuhasebeHesapBakiyeAsync(tesisId, donem, hesaplar.CashAccount, 1000m, 0m, "Borc", cancellationToken);
        await EnsureMuhasebeHesapBakiyeAsync(tesisId, donem, hesaplar.RevenueAccount, 0m, 800m, "Alacak", cancellationToken);
        await EnsureMuhasebeHesapBakiyeAsync(tesisId, donem, hesaplar.VatAccount, 0m, 200m, "Alacak", cancellationToken);
        await EnsureMuhasebeHesapBakiyeAsync(tesisId, donem, hesaplar.CustomerAccount, 1000m, 0m, "Borc", cancellationToken);
    }

    private async Task EnsureMuhasebeHesapBakiyeAsync(
        int tesisId,
        MuhasebeDonem donem,
        MuhasebeHesapPlani hesap,
        decimal borcToplam,
        decimal alacakToplam,
        string bakiyeTipi,
        CancellationToken cancellationToken)
    {
        var bakiye = await _db.Set<MuhasebeHesapBakiye>().FirstOrDefaultAsync(
            x => x.TesisId == tesisId &&
                 x.MaliYil == donem.MaliYil &&
                 x.Donem == donem.DonemNo &&
                 x.MuhasebeHesapPlaniId == hesap.Id &&
                 !x.KonsolideMi,
            cancellationToken);

        var netBakiye = borcToplam - alacakToplam;
        var sonGuncellemeTarihi = DateTime.UtcNow;

        if (bakiye is null)
        {
            _db.Set<MuhasebeHesapBakiye>().Add(new MuhasebeHesapBakiye
            {
                TesisId = tesisId,
                MaliYil = donem.MaliYil,
                Donem = donem.DonemNo,
                MuhasebeHesapPlaniId = hesap.Id,
                HesapKodu = hesap.Kod,
                HesapAdi = hesap.Ad,
                KonsolideMi = false,
                BorcToplam = borcToplam,
                AlacakToplam = alacakToplam,
                BorcBakiye = netBakiye > 0 ? netBakiye : 0m,
                AlacakBakiye = netBakiye < 0 ? Math.Abs(netBakiye) : 0m,
                NetBakiye = netBakiye,
                BakiyeTipi = bakiyeTipi,
                HesapSeviyesi = hesap.SeviyeNo,
                UstHesapKodu = null,
                SonGuncellemeTarihi = sonGuncellemeTarihi
            });
            return;
        }

        bakiye.HesapKodu = hesap.Kod;
        bakiye.HesapAdi = hesap.Ad;
        bakiye.BorcToplam = borcToplam;
        bakiye.AlacakToplam = alacakToplam;
        bakiye.BorcBakiye = netBakiye > 0 ? netBakiye : 0m;
        bakiye.AlacakBakiye = netBakiye < 0 ? Math.Abs(netBakiye) : 0m;
        bakiye.NetBakiye = netBakiye;
        bakiye.BakiyeTipi = bakiyeTipi;
        bakiye.HesapSeviyesi = hesap.SeviyeNo;
        bakiye.SonGuncellemeTarihi = sonGuncellemeTarihi;
    }

    private sealed record HesapPlaniSet(
        MuhasebeHesapPlani CashAccount,
        MuhasebeHesapPlani BankAccount,
        MuhasebeHesapPlani CustomerAccount,
        MuhasebeHesapPlani SupplierAccount,
        MuhasebeHesapPlani RevenueAccount,
        MuhasebeHesapPlani VatAccount,
        MuhasebeHesapPlani DiscountAccount,
        MuhasebeHesapPlani StockAccount);
}
