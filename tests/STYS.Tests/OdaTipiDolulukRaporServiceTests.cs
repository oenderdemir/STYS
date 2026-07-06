using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.OdaTipiDoluluk.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class OdaTipiDolulukRaporServiceTests
{
    private static readonly DateTime Baslangic = new(2026, 7, 1);
    private static readonly DateTime Bitis = new(2026, 7, 7);

    // Rezervasyon yoksa doluluk orani 0 olur, musaitlik orani 100 olur.
    [Fact]
    public async Task GetRaporAsync_RezervasyonYokIseDolulukSifirMusaitlikYuzYuzOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis);

        Assert.All(rapor.OdaTipleri, x => Assert.Equal(0m, x.DolulukOrani));
        Assert.All(rapor.OdaTipleri, x => Assert.Equal(100m, x.MusaitlikOrani));
    }

    // Secilen aralikta tum gunleri dolu olan oda tipi doluluk orani 100 olur.
    [Fact]
    public async Task GetRaporAsync_TumAralikDoluOlanOdaTipiYuzYuzDolulukOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(100m, odaTipi.DolulukOrani);
    }

    // Bazi gunleri dolu bazi gunleri bos olan oda tipi kismi doluluk uretir.
    [Fact]
    public async Task GetRaporAsync_KismiDoluOdaTipiKismiDolulukUretir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(3));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.True(odaTipi.DolulukOrani > 0m && odaTipi.DolulukOrani < 100m);
    }

    // Cikis gunu dolu sayilmaz.
    [Fact]
    public async Task GetRaporAsync_CikisGunuDoluSayilmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        var oda = Assert.Single(odaTipi.Odalar);
        Assert.Equal(2, oda.DoluGunSayisi);
    }

    // Iptal rezervasyon doluluk yaratmaz.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonDolulukYaratmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(0, odaTipi.DoluOdaGunSayisi);
    }

    // Silinmis segment/oda atamasi doluluk yaratmaz.
    [Fact]
    public async Task GetRaporAsync_SilinmisOdaAtamasiDolulukYaratmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (_, _, atama) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        dbContext.RezervasyonSegmentOdaAtamalari.Remove(atama);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(0, odaTipi.DoluOdaGunSayisi);
    }

    // odaTipiId filtresi sadece ilgili oda tipini getirir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdFiltresiSadeceIlgiliOdaTipiniGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 21);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(21, odaTipi.OdaTipiId);
    }

    // odaTipiId verilince OdaTipiAdi, filtrelenmis (tesis/aktif) odalardan gelmeli.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdVerilinceOdaTipiAdiFiltrelenmisOdalardanGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 21);

        Assert.Equal("Suit", rapor.OdaTipiAdi);
    }

    // Ilgili odaTipiId icin tesiste aktif oda yoksa rapor bos gelir ve OdaTipiAdi null kalir.
    [Fact]
    public async Task GetRaporAsync_TesisteAktifOdaOlmayanOdaTipiIcinRaporBosGelirVeOdaTipiAdiNullKalir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 999);

        Assert.Empty(rapor.OdaTipleri);
        Assert.Null(rapor.OdaTipiAdi);
    }

    // ToplamOdaGunSayisi dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_ToplamOdaGunSayisiDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        // Bu tipte 1 oda var (Id=100), aralik 7 gun -> 1*7=7.
        Assert.Equal(7, odaTipi.ToplamOdaGunSayisi);
    }

    // DoluOdaGunSayisi oda/gun bazinda tek sayilir (ayni oda/gun icin birden fazla rezervasyon olsa bile).
    [Fact]
    public async Task GetRaporAsync_DoluOdaGunSayisiOdaGunBazindaTekSayilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        // Ayni oda/gun'e cakisan iki ayri rezervasyon (veri hatasi/cakisma senaryosu).
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2));
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        var oda = Assert.Single(odaTipi.Odalar);
        Assert.Equal(2, oda.DoluGunSayisi);
    }

    // ToplamRezervasyonSayisi ayni rezervasyonu ayni oda tipinde tek sayar.
    [Fact]
    public async Task GetRaporAsync_ToplamRezervasyonSayisiAyniRezervasyonuTekSayar()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var rezervasyon = await SeedRezervasyonDetayliVeIkinciSegmentAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2), ikinciSegmentGiris: Baslangic.AddDays(2), ikinciSegmentCikis: Baslangic.AddDays(4));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(1, odaTipi.ToplamRezervasyonSayisi);
        Assert.Equal(rezervasyon.Id, rezervasyon.Id);
    }

    // ToplamKonaklayanKisiSayisi ayni rezervasyonu ayni oda tipinde tek sayar.
    [Fact]
    public async Task GetRaporAsync_ToplamKonaklayanKisiSayisiAyniRezervasyonuTekSayar()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonDetayliVeIkinciSegmentAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2), ikinciSegmentGiris: Baslangic.AddDays(2), ikinciSegmentCikis: Baslangic.AddDays(4), kisiSayisi: 3);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, odaTipiId: 20);

        var odaTipi = Assert.Single(rapor.OdaTipleri);
        Assert.Equal(3, odaTipi.ToplamKonaklayanKisiSayisi);
    }

    // Ozet, satir toplamlariyla uyumludur.
    [Fact]
    public async Task GetRaporAsync_OzetSatirToplamlariylaUyumludur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis);

        Assert.Equal(rapor.OdaTipleri.Sum(x => x.OdaSayisi), rapor.Ozet.ToplamOdaSayisi);
        Assert.Equal(rapor.OdaTipleri.Sum(x => x.ToplamKapasite), rapor.Ozet.ToplamKapasite);
        Assert.Equal(rapor.OdaTipleri.Sum(x => x.DoluOdaGunSayisi), rapor.Ozet.DoluOdaGunSayisi);
        Assert.Equal(rapor.OdaTipleri.Sum(x => x.BosOdaGunSayisi), rapor.Ozet.BosOdaGunSayisi);
    }

    // Yetkisiz tesis icin 403 doner.
    [Fact]
    public async Task GetRaporAsync_YetkisizTesisIcin403Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = new OdaTipiDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(scoped: true, izinliTesisIds: [999]),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetRaporAsync(1, Baslangic, Bitis));

        Assert.Equal(403, exception.ErrorCode);
    }

    private static OdaTipiDolulukRaporService CreateService(StysAppDbContext dbContext)
    {
        return new OdaTipiDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
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

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 21,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Suit",
            Kapasite = 4,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda { Id = 100, OdaNo = "101", BinaId = 10, TesisOdaTipiId = 20, KatNo = 1, AktifMi = true });
        dbContext.Odalar.Add(new Oda { Id = 101, OdaNo = "201", BinaId = 10, TesisOdaTipiId = 21, KatNo = 2, AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli)
    {
        await SeedRezervasyonDetayliAsync(dbContext, odaId, girisTarihi, cikisTarihi, rezervasyonDurumu);
    }

    private static async Task<(Rezervasyon Rezervasyon, RezervasyonSegment Segment, RezervasyonSegmentOdaAtama Atama)> SeedRezervasyonDetayliAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli,
        int kisiSayisi = 2)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = kisiSayisi,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = "Test Misafir",
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

        var atama = new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = segment.Id,
            OdaId = odaId,
            AyrilanKisiSayisi = kisiSayisi,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        };
        dbContext.RezervasyonSegmentOdaAtamalari.Add(atama);
        await dbContext.SaveChangesAsync();

        return (rezervasyon, segment, atama);
    }

    private static async Task<Rezervasyon> SeedRezervasyonDetayliVeIkinciSegmentAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        DateTime ikinciSegmentGiris,
        DateTime ikinciSegmentCikis,
        int kisiSayisi = 2)
    {
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId, girisTarihi, cikisTarihi, kisiSayisi: kisiSayisi);

        var ikinciSegment = new RezervasyonSegment
        {
            RezervasyonId = rezervasyon.Id,
            SegmentSirasi = 1,
            BaslangicTarihi = ikinciSegmentGiris,
            BitisTarihi = ikinciSegmentCikis
        };
        dbContext.RezervasyonSegmentleri.Add(ikinciSegment);
        await dbContext.SaveChangesAsync();

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = ikinciSegment.Id,
            OdaId = odaId,
            AyrilanKisiSayisi = kisiSayisi,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        });
        await dbContext.SaveChangesAsync();

        return rezervasyon;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly bool _scoped;
        private readonly int[] _izinliTesisIds;

        public FakeUserAccessScopeService(bool scoped = false, int[]? izinliTesisIds = null)
        {
            _scoped = scoped;
            _izinliTesisIds = izinliTesisIds ?? [];
        }

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scoped ? DomainAccessScope.Scoped([], _izinliTesisIds, []) : DomainAccessScope.Unscoped());
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
