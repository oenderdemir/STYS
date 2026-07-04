using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.KonaklamaKisiSayisi.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Tests;

public class KonaklamaKisiSayisiRaporServiceTests
{
    // Rezervasyon olmayan ayda tum oda hucreleri 0 donmeli.
    [Fact]
    public async Task GetRaporAsync_RezervasyonYokIseHucrelerSifirDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2026, 2026);

        var yilSatiri = Assert.Single(rapor.Yillar);
        Assert.All(yilSatiri.Hucreler, h => Assert.Equal(0, h.KisiSayisi));
        Assert.Equal(0, yilSatiri.ToplamKisiSayisi);
    }

    // 3 gece kalan tek rezervasyon kisi sayisini bir kez sayar (gece bazinda cogaltmaz).
    [Fact]
    public async Task GetRaporAsync_UcGeceKalanRezervasyonBirKezSayilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 5, 10),
            cikisTarihi: new DateTime(2026, 5, 13),
            ayrilanKisiSayisi: 2,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2026, 2026);

        var yilSatiri = Assert.Single(rapor.Yillar);
        var hucre = yilSatiri.Hucreler.Single(h => h.OdaId == 100);

        Assert.Equal(2, hucre.KisiSayisi);
        Assert.Equal(2, yilSatiri.ToplamKisiSayisi);
    }

    // Ay disina tasan rezervasyon da (segment ayla cakistigi surece) o ay icin bir kez sayilir.
    [Fact]
    public async Task GetRaporAsync_AyDisinaTasanRezervasyonSadeceCakisanAyaDahilOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 4, 29),
            cikisTarihi: new DateTime(2026, 5, 3),
            ayrilanKisiSayisi: 1,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var mayisRaporu = await service.GetRaporAsync(1, 5, 2026, 2026);
        var nisanRaporu = await service.GetRaporAsync(1, 4, 2026, 2026);

        Assert.Equal(1, mayisRaporu.Yillar.Single().Hucreler.Single(h => h.OdaId == 100).KisiSayisi);
        Assert.Equal(1, nisanRaporu.Yillar.Single().Hucreler.Single(h => h.OdaId == 100).KisiSayisi);
    }

    // Iptal rezervasyon kisi sayisina dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 5, 10),
            cikisTarihi: new DateTime(2026, 5, 13),
            ayrilanKisiSayisi: 3,
            rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2026, 2026);

        Assert.Equal(0, rapor.Yillar.Single().Hucreler.Single(h => h.OdaId == 100).KisiSayisi);
    }

    // Ayni rezervasyon/oda ayda birden fazla segmentte gorunse bile bir kez sayilir.
    [Fact]
    public async Task GetRaporAsync_AyniRezervasyonOdaMukerrerSegmenttebirKezSayilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var rezervasyon = new Rezervasyon
        {
            ReferansNo = "REF-MUKERRER",
            TesisId = 1,
            KisiSayisi = 2,
            GirisTarihi = new DateTime(2026, 5, 5),
            CikisTarihi = new DateTime(2026, 5, 15),
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = "Test Misafir",
            MisafirTelefon = "5550000000",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();

        // Ayni rezervasyon/oda icin iki ayri segment (ornegin oda degisimi sonrasi geri donus).
        var segment1 = new RezervasyonSegment { RezervasyonId = rezervasyon.Id, SegmentSirasi = 0, BaslangicTarihi = new DateTime(2026, 5, 5), BitisTarihi = new DateTime(2026, 5, 10) };
        var segment2 = new RezervasyonSegment { RezervasyonId = rezervasyon.Id, SegmentSirasi = 1, BaslangicTarihi = new DateTime(2026, 5, 10), BitisTarihi = new DateTime(2026, 5, 15) };
        dbContext.RezervasyonSegmentleri.AddRange(segment1, segment2);
        await dbContext.SaveChangesAsync();

        dbContext.RezervasyonSegmentOdaAtamalari.AddRange(
            new RezervasyonSegmentOdaAtama { RezervasyonSegmentId = segment1.Id, OdaId = 100, AyrilanKisiSayisi = 2, OdaNoSnapshot = "101", BinaAdiSnapshot = "Bina-1", OdaTipiAdiSnapshot = "Standart", KapasiteSnapshot = 2 },
            new RezervasyonSegmentOdaAtama { RezervasyonSegmentId = segment2.Id, OdaId = 100, AyrilanKisiSayisi = 2, OdaNoSnapshot = "101", BinaAdiSnapshot = "Bina-1", OdaTipiAdiSnapshot = "Standart", KapasiteSnapshot = 2 });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2026, 2026);

        Assert.Equal(2, rapor.Yillar.Single().Hucreler.Single(h => h.OdaId == 100).KisiSayisi);
    }

    // Farkli yillar icin ayri satirlar dogru olusur.
    [Fact]
    public async Task GetRaporAsync_FarkliYillarIcinAyriSatirlarOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2025, 5, 10),
            cikisTarihi: new DateTime(2025, 5, 12),
            ayrilanKisiSayisi: 1,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 101,
            girisTarihi: new DateTime(2026, 5, 10),
            cikisTarihi: new DateTime(2026, 5, 12),
            ayrilanKisiSayisi: 4,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2025, 2026);

        Assert.Equal(2, rapor.Yillar.Count);
        var satir2025 = rapor.Yillar.Single(y => y.Yil == 2025);
        var satir2026 = rapor.Yillar.Single(y => y.Yil == 2026);

        Assert.Equal(1, satir2025.ToplamKisiSayisi);
        Assert.Equal(4, satir2026.ToplamKisiSayisi);
    }

    // ToplamKisiSayisi oda hucrelerinin toplamina esit olmali.
    [Fact]
    public async Task GetRaporAsync_ToplamKisiSayisiHucreToplaminaEsitOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 5, 10),
            cikisTarihi: new DateTime(2026, 5, 12),
            ayrilanKisiSayisi: 2,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 101,
            girisTarihi: new DateTime(2026, 5, 10),
            cikisTarihi: new DateTime(2026, 5, 12),
            ayrilanKisiSayisi: 3,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, 5, 2026, 2026);

        var yilSatiri = rapor.Yillar.Single();
        Assert.Equal(yilSatiri.Hucreler.Sum(h => h.KisiSayisi), yilSatiri.ToplamKisiSayisi);
    }

    // Baslik formati dogru olusmali: farkli yil araliginda ve tek yilda.
    [Fact]
    public async Task GetRaporAsync_BaslikFormatiDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var farkliYilRaporu = await service.GetRaporAsync(1, 5, 2025, 2026);
        var tekYilRaporu = await service.GetRaporAsync(1, 5, 2026, 2026);

        Assert.Equal("2025-2026 MAYIS AYI KONAKLAYAN KİŞİ SAYISI", farkliYilRaporu.Baslik);
        Assert.Equal("2026 MAYIS AYI KONAKLAYAN KİŞİ SAYISI", tekYilRaporu.Baslik);
    }

    private static KonaklamaKisiSayisiRaporService CreateService(StysAppDbContext dbContext)
    {
        return new KonaklamaKisiSayisiRaporService(
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

        dbContext.Odalar.Add(new Oda { Id = 100, OdaNo = "101", BinaId = 10, TesisOdaTipiId = 20, KatNo = 1, AktifMi = true });
        dbContext.Odalar.Add(new Oda { Id = 101, OdaNo = "102", BinaId = 10, TesisOdaTipiId = 20, KatNo = 1, AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        int ayrilanKisiSayisi,
        string rezervasyonDurumu)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = ayrilanKisiSayisi,
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

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = segment.Id,
            OdaId = odaId,
            AyrilanKisiSayisi = ayrilanKisiSayisi,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
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
