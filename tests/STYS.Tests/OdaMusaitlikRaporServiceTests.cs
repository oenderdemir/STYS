using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.OdaMusaitlik.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class OdaMusaitlikRaporServiceTests
{
    private static readonly DateTime Baslangic = new(2026, 7, 1);
    private static readonly DateTime Bitis = new(2026, 7, 7);

    // Rezervasyon yoksa tum odalar tamamen-bos olur.
    [Fact]
    public async Task GetRaporAsync_RezervasyonYokIseTumOdalarTamamenBosOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        Assert.All(rapor.Odalar, o => Assert.Equal("tamamen-bos", o.MusaitlikDurumu));
        Assert.Equal(2, rapor.Ozet.TamamenBosOdaSayisi);
    }

    // Secilen araliktaki tum gunleri dolu olan oda tamamen-dolu olur.
    [Fact]
    public async Task GetRaporAsync_TumAralikDoluOlanOdaTamamenDoluOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        var oda = rapor.Odalar.Single(x => x.OdaId == 100);
        Assert.Equal("tamamen-dolu", oda.MusaitlikDurumu);
        Assert.Equal(7, oda.DoluGunSayisi);
        Assert.Equal(0, oda.BosGunSayisi);
    }

    // Bazi gunleri dolu bazi gunleri bos olan oda kismen-musait olur.
    [Fact]
    public async Task GetRaporAsync_KismiDoluOdaKismenMusaitOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(3), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        var oda = rapor.Odalar.Single(x => x.OdaId == 100);
        Assert.Equal("kismen-musait", oda.MusaitlikDurumu);
        Assert.Equal(3, oda.DoluGunSayisi);
        Assert.Equal(4, oda.BosGunSayisi);
    }

    // Cikis gunu dolu sayilmaz.
    [Fact]
    public async Task GetRaporAsync_CikisGunuDoluSayilmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        var oda = rapor.Odalar.Single(x => x.OdaId == 100);
        var cikisGunu = oda.Gunler.Single(g => g.Tarih == Baslangic.AddDays(2));
        Assert.True(cikisGunu.BosMu);
        Assert.False(cikisGunu.DoluMu);
    }

    // Iptal rezervasyon doluluk yaratmaz.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonDolulukYaratmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        var oda = rapor.Odalar.Single(x => x.OdaId == 100);
        Assert.Equal("tamamen-bos", oda.MusaitlikDurumu);
    }

    // Silinmis segment/oda atamasi doluluk yaratmaz.
    [Fact]
    public async Task GetRaporAsync_SilinmisOdaAtamasiDolulukYaratmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (_, segment, atama) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        dbContext.RezervasyonSegmentOdaAtamalari.Remove(atama);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu");

        var oda = rapor.Odalar.Single(x => x.OdaId == 100);
        Assert.Equal("tamamen-bos", oda.MusaitlikDurumu);
    }

    // durum=tamamen-bos sadece tamamen bos odalari getirir.
    [Fact]
    public async Task GetRaporAsync_TamamenBosFiltresiSadeceTamamenBosOdalariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tamamen-bos");

        var oda = Assert.Single(rapor.Odalar);
        Assert.Equal(101, oda.OdaId);
    }

    // durum=tamamen-dolu sadece tamamen dolu odalari getirir.
    [Fact]
    public async Task GetRaporAsync_TamamenDoluFiltresiSadeceTamamenDoluOdalariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tamamen-dolu");

        var oda = Assert.Single(rapor.Odalar);
        Assert.Equal(100, oda.OdaId);
    }

    // durum=kismen-musait sadece kismen musait odalari getirir.
    [Fact]
    public async Task GetRaporAsync_KismenMusaitFiltresiSadeceKismenMusaitOdalariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Baslangic.AddDays(3), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "kismen-musait");

        var oda = Assert.Single(rapor.Odalar);
        Assert.Equal(100, oda.OdaId);
    }

    // Oda tipi filtresi calisir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiFiltresiCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu", odaTipiId: 21);

        var oda = Assert.Single(rapor.Odalar);
        Assert.Equal(101, oda.OdaId);
    }

    // Kapasite filtresi calisir.
    [Fact]
    public async Task GetRaporAsync_KapasiteFiltresiCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu", kapasite: 3);

        var oda = Assert.Single(rapor.Odalar);
        Assert.Equal(101, oda.OdaId);
    }

    // Ozet, filtre sonrasi listeyle uyumlu olur.
    [Fact]
    public async Task GetRaporAsync_OzetFiltreSonrasiListeyleUyumluOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tamamen-dolu");

        Assert.Equal(1, rapor.Ozet.ToplamOdaSayisi);
        Assert.Equal(1, rapor.Ozet.TamamenDoluOdaSayisi);
        Assert.Equal(0, rapor.Ozet.TamamenBosOdaSayisi);
        Assert.Equal(rapor.Odalar.Sum(x => x.BosGunSayisi), rapor.Ozet.BosOdaGunSayisi);
    }

    // 60 gunluk (dahil) tarih araligi kabul edilir.
    [Fact]
    public async Task GetRaporAsync_AltmisGunlukAralikKabulEdilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var altmisGunlukBitis = Baslangic.AddDays(59);
        var rapor = await service.GetRaporAsync(1, Baslangic, altmisGunlukBitis, "tumu");

        Assert.Equal(60, rapor.Ozet.ToplamGunSayisi);
    }

    // 61 gunluk (dahil) tarih araligi hata verir.
    [Fact]
    public async Task GetRaporAsync_AltmisBirGunlukAralikHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var altmisBirGunlukBitis = Baslangic.AddDays(60);

        var exception = await Assert.ThrowsAsync<BaseException>(
            () => service.GetRaporAsync(1, Baslangic, altmisBirGunlukBitis, "tumu"));

        Assert.Equal(400, exception.ErrorCode);
    }

    // OdaTipiAdi filtre verilince (odaTipiId ile) dolu gelir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdVerilinceOdaTipiAdiDoluGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, Baslangic, Bitis, "tumu", odaTipiId: 21);

        Assert.Equal("Suit", rapor.OdaTipiAdi);
    }

    // Yetkisiz tesis icin 403 doner.
    [Fact]
    public async Task GetRaporAsync_YetkisizTesisIcin403Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = new OdaMusaitlikRaporService(
            dbContext,
            new FakeUserAccessScopeService(scoped: true, izinliTesisIds: [999]),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetRaporAsync(1, Baslangic, Bitis, "tumu"));

        Assert.Equal(403, exception.ErrorCode);
    }

    private static OdaMusaitlikRaporService CreateService(StysAppDbContext dbContext)
    {
        return new OdaMusaitlikRaporService(
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
        dbContext.Odalar.Add(new Oda { Id = 101, OdaNo = "102", BinaId = 10, TesisOdaTipiId = 21, KatNo = 1, AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string rezervasyonDurumu)
    {
        await SeedRezervasyonDetayliAsync(dbContext, odaId, girisTarihi, cikisTarihi, rezervasyonDurumu);
    }

    private static async Task<(Rezervasyon Rezervasyon, RezervasyonSegment Segment, RezervasyonSegmentOdaAtama Atama)> SeedRezervasyonDetayliAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string rezervasyonDurumu)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = 2,
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
            AyrilanKisiSayisi = 2,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        };
        dbContext.RezervasyonSegmentOdaAtamalari.Add(atama);
        await dbContext.SaveChangesAsync();

        return (rezervasyon, segment, atama);
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
