using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class OdaDolulukRaporServiceTests
{
    // Rezervasyon olmayan bir ayda tum hucreler bos donmeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_BosAydaTumHucrelerBosDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        Assert.Equal(31, rapor.Gunler.Count);
        Assert.All(rapor.Gunler, gun => Assert.All(gun.Hucreler, hucre => Assert.False(hucre.DoluMu)));
        Assert.Equal(0, rapor.Ozet.DoluOdaGunSayisi);
        Assert.Equal(0m, rapor.Ozet.DolulukOraniYuzde);
    }

    // 3 gece kalan bir rezervasyon giris gunlerinde dogru odada dolu gorunmeli, cikis gunu dolu sayilmamali.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_UcGeceKalanRezervasyonDogruGunlerdeDoluGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        var odaGunleri = rapor.Gunler.ToDictionary(g => g.Tarih, g => g.Hucreler.Single(h => h.OdaId == 100));

        Assert.True(odaGunleri[new DateTime(2026, 7, 10)].DoluMu);
        Assert.True(odaGunleri[new DateTime(2026, 7, 11)].DoluMu);
        Assert.True(odaGunleri[new DateTime(2026, 7, 12)].DoluMu);

        // Cikis gunu (13 Temmuz) dolu sayilmamali.
        Assert.False(odaGunleri[new DateTime(2026, 7, 13)].DoluMu);

        Assert.Equal(3, rapor.Ozet.DoluOdaGunSayisi);
    }

    // Iptal edilen rezervasyon dolulukta hic sayilmamali.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_IptalRezervasyonDolulukSayilmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        Assert.All(rapor.Gunler, gun => Assert.All(gun.Hucreler, hucre => Assert.False(hucre.DoluMu)));
        Assert.Equal(0, rapor.Ozet.DoluOdaGunSayisi);
    }

    // Odenen tutar toplam ucretten az ise hucrede OdemesiEksikMi true olmali.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_OdemeEksikseOdemesiEksikMiTrueOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 100m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        var hucre = rapor.Gunler.Single(g => g.Tarih == new DateTime(2026, 7, 10)).Hucreler.Single(h => h.OdaId == 100);

        Assert.True(hucre.OdemesiEksikMi);
        Assert.Equal(200m, hucre.KalanTutar);
        Assert.Equal("payment-missing", hucre.HucreRenkKodu);
    }

    // maskele=true iken misafir adi soyadi maskelenmeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_MaskeleTrueIseMisafirAdiMaskelenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "ÖNDER DEMİR");

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: true);

        var hucre = rapor.Gunler.Single(g => g.Tarih == new DateTime(2026, 7, 10)).Hucreler.Single(h => h.OdaId == 100);

        Assert.Equal("Ö**** D****", hucre.MisafirAdiSoyadi);
    }

    // AyIcindeTahsilEdilenTutar sadece odeme tarihi rapor ayi icinde olan odemeleri toplamali;
    // KonaklayanRezervasyonlarinToplamTahsilati ise odeme tarihinden bagimsiz tum odemeleri icermeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_AyIcindeTahsilEdilenSadeceOAyinOdemeleriniToplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var rezervasyonId = await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 6, 25), 100m);
        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 7, 10), 200m);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        Assert.Equal(200m, rapor.Ozet.AyIcindeTahsilEdilenTutar);
        Assert.Equal(300m, rapor.Ozet.KonaklayanRezervasyonlarinToplamTahsilati);
        Assert.Equal(200m, rapor.Ozet.ToplamTahsilat);
    }

    // Ayni oda/gun icin iki rezervasyon varsa hucre cakisma bilgisi tasimali.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_AyniOdaGunIkiRezervasyonVarsaCakismaBilgisiDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 9),
            cikisTarihi: new DateTime(2026, 7, 11),
            toplamUcret: 200m,
            odemeTutari: 200m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ayşe Yılmaz");

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        var hucre = rapor.Gunler.Single(g => g.Tarih == new DateTime(2026, 7, 10)).Hucreler.Single(h => h.OdaId == 100);

        Assert.True(hucre.CakismaVarMi);
        Assert.Equal(2, hucre.CakismaSayisi);
        Assert.Equal(2, hucre.Cakismalar.Count);
        Assert.Equal("conflict", hucre.HucreRenkKodu);
    }

    // maskele=true iken cakisma listesindeki misafir adlari da maskelenmeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_MaskeleTrueIseCakismaMisafirAdlariMaskelenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "ÖNDER DEMİR");
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 9),
            cikisTarihi: new DateTime(2026, 7, 11),
            toplamUcret: 200m,
            odemeTutari: 200m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ayşe Yılmaz");

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: true);

        var hucre = rapor.Gunler.Single(g => g.Tarih == new DateTime(2026, 7, 10)).Hucreler.Single(h => h.OdaId == 100);

        Assert.True(hucre.CakismaVarMi);
        Assert.All(hucre.Cakismalar, c => Assert.DoesNotContain(c.MisafirAdiSoyadi, new[] { "ÖNDER DEMİR", "Ayşe Yılmaz" }));
        Assert.Contains(hucre.Cakismalar, c => c.MisafirAdiSoyadi == "Ö**** D****");
    }

    // Rapor ayi icindeki odeme Tahsilatlar listesine gelmeli, ay disindaki odeme gelmemeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_TahsilatlarSadeceAyIcindekiOdemeleriIcerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var rezervasyonId = await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 6, 25), 100m);
        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 7, 10), 200m);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        var tahsilat = Assert.Single(rapor.Tahsilatlar);
        Assert.Equal(200m, tahsilat.OdemeTutari);
        Assert.Equal(new DateTime(2026, 7, 10), tahsilat.OdemeTarihi);
        Assert.Equal("101", tahsilat.OdaNo);
        Assert.Equal("Ali Veli", tahsilat.MisafirAdiSoyadi);
        Assert.Equal("Ali Veli", tahsilat.OdemeYapan);
    }

    // Tahsilatlar toplami Ozet.AyIcindeTahsilEdilenTutar ile uyumlu olmali.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_TahsilatlarToplamiOzetIleUyumluOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var rezervasyonId1 = await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 5),
            cikisTarihi: new DateTime(2026, 7, 7),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");
        var rezervasyonId2 = await SeedRezervasyonAsync(
            dbContext,
            odaId: 101,
            girisTarihi: new DateTime(2026, 7, 15),
            cikisTarihi: new DateTime(2026, 7, 17),
            toplamUcret: 250m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ayşe Yılmaz");

        await AddOdemeAsync(dbContext, rezervasyonId1, new DateTime(2026, 7, 5), 150m);
        await AddOdemeAsync(dbContext, rezervasyonId2, new DateTime(2026, 7, 15), 250m);
        await AddOdemeAsync(dbContext, rezervasyonId2, new DateTime(2026, 6, 1), 50m);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: false);

        Assert.Equal(2, rapor.Tahsilatlar.Count);
        Assert.Equal(rapor.Ozet.AyIcindeTahsilEdilenTutar, rapor.Tahsilatlar.Sum(x => x.OdemeTutari));
    }

    // maskele=true iken tahsilat listesindeki odeme yapan/misafir adi maskelenmeli.
    [Fact]
    public async Task GetAylikOdaDolulukRaporuAsync_MaskeleTrueIseTahsilatOdemeYapanMaskelenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var rezervasyonId = await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "ÖNDER DEMİR");

        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 7, 10), 300m);

        var service = CreateService(dbContext);
        var rapor = await service.GetAylikOdaDolulukRaporuAsync(1, 2026, 7, maskele: true);

        var tahsilat = Assert.Single(rapor.Tahsilatlar);
        Assert.Equal("Ö**** D****", tahsilat.MisafirAdiSoyadi);
        Assert.Equal("Ö**** D****", tahsilat.OdemeYapan);
    }

    private static OdaDolulukRaporService CreateService(StysAppDbContext dbContext)
    {
        return new OdaDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeLicenseService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase($"stys-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, null, new FakeCurrentTenantAccessor());
    }

    private static async Task SeedOdaFixtureAsync(StysAppDbContext dbContext)
    {
        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            Ad = "Test Tesis",
            KurumId = 1,
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 10,
            TesisId = 1,
            Ad = "Bina-1",
            KatSayisi = 3,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 20,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Standart",
            Kapasite = 2,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 100,
            OdaNo = "101",
            BinaId = 10,
            TesisOdaTipiId = 20,
            KatNo = 1,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 101,
            OdaNo = "102",
            BinaId = 10,
            TesisOdaTipiId = 20,
            KatNo = 1,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        decimal toplamUcret,
        decimal odemeTutari,
        string rezervasyonDurumu,
        string misafirAdiSoyadi = "Test Misafir")
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            ToplamBazUcret = toplamUcret,
            ToplamUcret = toplamUcret,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = misafirAdiSoyadi,
            MisafirTelefon = "5550000000",
            RezervasyonDurumu = rezervasyonDurumu,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();

        var segment = new RezervasyonSegment
        {
            RezervasyonId = rezervasyon.Id,
            SegmentSirasi = 0,
            BaslangicTarihi = girisTarihi,
            BitisTarihi = cikisTarihi
        };
        dbContext.RezervasyonSegmentleri.Add(segment);
        await dbContext.SaveChangesAsync();

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = segment.Id,
            OdaId = odaId,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        });

        if (odemeTutari > 0m)
        {
            dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
            {
                RezervasyonId = rezervasyon.Id,
                OdemeTarihi = girisTarihi,
                OdemeTutari = odemeTutari,
                ParaBirimi = "TRY",
                OdemeTipi = OdemeTipleri.Nakit
            });
        }

        await dbContext.SaveChangesAsync();

        return rezervasyon.Id;
    }

    private static async Task AddOdemeAsync(StysAppDbContext dbContext, int rezervasyonId, DateTime odemeTarihi, decimal odemeTutari)
    {
        dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
        {
            RezervasyonId = rezervasyonId,
            OdemeTarihi = odemeTarihi,
            OdemeTutari = odemeTutari,
            ParaBirimi = "TRY",
            OdemeTipi = OdemeTipleri.Nakit
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeLicenseService : ILicenseService
    {
        public Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LicenseValidationResult.Failure("test"));

        public Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public void InvalidateCache()
        {
        }

        public Task EnsureLicensedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [];

        public bool IsSuperAdmin() => true;

        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload)
        {
        }

        public void Completed(string eventName, object payload)
        {
        }

        public void Warning(string eventName, object payload)
        {
        }

        public void Failed(string eventName, Exception exception, object payload)
        {
        }
    }
}
