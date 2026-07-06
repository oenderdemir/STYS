using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.GecikenCheckIn.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class GecikenCheckInRaporServiceTests
{
    private static readonly DateTime ReferansTarihi = new(2026, 7, 10);

    // Bugun giris tarihli Onayli rezervasyon "bugun-giris" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_BugunGirisTarihliOnayliRezervasyonBugunGirisOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("bugun-giris", rezervasyon.GecikmeDurumu);
        Assert.Equal(0, rezervasyon.GecikenGunSayisi);
    }

    // Giris tarihi 1 gun gecmis Onayli rezervasyon "geciken" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_BirGunGecikenOnayliRezervasyonGecikenOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-1), cikisTarihi: ReferansTarihi.AddDays(3), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("geciken", rezervasyon.GecikmeDurumu);
        Assert.Equal(1, rezervasyon.GecikenGunSayisi);
    }

    // Giris tarihi 3 gun gecmis Onayli rezervasyon "kritik-geciken" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_UcGunGecikenOnayliRezervasyonKritikGecikenOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-3), cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("kritik-geciken", rezervasyon.GecikmeDurumu);
        Assert.Equal(3, rezervasyon.GecikenGunSayisi);
    }

    // CheckInTamamlandi rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_CheckInTamamlandiRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-1), cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.CheckInTamamlandi);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // CheckOutTamamlandi rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_CheckOutTamamlandiRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-2), cikisTarihi: ReferansTarihi.AddDays(-1), rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Iptal rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-1), cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Soft-delete rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_SilinmisRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-1), cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        rezervasyon.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Aktif olmayan rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_AktifOlmayanRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-1), cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        rezervasyon.AktifMi = false;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Gelecek giris tarihli rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_GelecekGirisTarihliRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(1), cikisTarihi: ReferansTarihi.AddDays(3), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // gecikmeDurumu filtresi dogru calisir.
    [Fact]
    public async Task GetRaporAsync_GecikmeDurumuFiltresiDogruCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-BUGUN");
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi.AddDays(-3), cikisTarihi: ReferansTarihi.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-KRITIK");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi, gecikmeDurumu: "kritik-geciken");

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-KRITIK", rezervasyon.ReferansNo);
    }

    // odaTipiId filtresi sadece ilgili oda tipine atanmis rezervasyonlari getirir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdFiltresiSadeceIlgiliOdaTipiniGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-STANDART");
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-SUIT");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi, odaTipiId: 21);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-SUIT", rezervasyon.ReferansNo);
    }

    // odaTipiId filtresinde DTO OdaTipleri sadece filtrelenen oda tipini gosterir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdFiltresindeOdaTipleriSadeceFiltrelenenOdaTipiniGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonIkiOdaTipindeAsync(dbContext, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(4));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi, odaTipiId: 20);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(["Standart"], rezervasyon.OdaTipleri);
    }

    // Odeme toplami ve kalan tutar dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_OdemeToplamiVeKalanTutarDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli, toplamUcret: 1000m);

        dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
        {
            RezervasyonId = rezervasyon.Id,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = 400m,
            ParaBirimi = "TRY",
            OdemeTipi = OdemeTipleri.Nakit
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(400m, kayit.OdenenTutar);
        Assert.Equal(600m, kayit.KalanTutar);
    }

    // Ozet rezervasyon listesiyle uyumludur.
    [Fact]
    public async Task GetRaporAsync_OzetRezervasyonListesiyleUyumludur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: ReferansTarihi, cikisTarihi: ReferansTarihi.AddDays(2), rezervasyonDurumu: RezervasyonDurumlari.Onayli, kisiSayisi: 2);
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: ReferansTarihi.AddDays(-3), cikisTarihi: ReferansTarihi.AddDays(1), rezervasyonDurumu: RezervasyonDurumlari.Onayli, kisiSayisi: 3);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, ReferansTarihi);

        Assert.Equal(2, rapor.Ozet.ToplamRezervasyonSayisi);
        Assert.Equal(1, rapor.Ozet.BugunGirisSayisi);
        Assert.Equal(1, rapor.Ozet.KritikGecikenSayisi);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.KisiSayisi), rapor.Ozet.ToplamKisiSayisi);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.KalanTutar), rapor.Ozet.ToplamKalanTutar);
    }

    // Yetkisiz tesis icin 403 doner.
    [Fact]
    public async Task GetRaporAsync_YetkisizTesisIcin403Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = new GecikenCheckInRaporService(
            dbContext,
            new FakeUserAccessScopeService(scoped: true, izinliTesisIds: [999]),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetRaporAsync(1, ReferansTarihi));

        Assert.Equal(403, exception.ErrorCode);
    }

    private static GecikenCheckInRaporService CreateService(StysAppDbContext dbContext)
    {
        return new GecikenCheckInRaporService(
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
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli,
        string? referansNo = null,
        int kisiSayisi = 2)
    {
        await SeedRezervasyonDetayliAsync(dbContext, odaId, girisTarihi, cikisTarihi, rezervasyonDurumu, referansNo, kisiSayisi);
    }

    private static async Task<(Rezervasyon Rezervasyon, RezervasyonSegment Segment, RezervasyonSegmentOdaAtama Atama)> SeedRezervasyonDetayliAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli,
        string? referansNo = null,
        int kisiSayisi = 2,
        decimal toplamUcret = 1000m)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = referansNo ?? $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = kisiSayisi,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            ToplamBazUcret = toplamUcret,
            ToplamUcret = toplamUcret,
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

    // Tek rezervasyon, iki ayri segmentte iki farkli oda tipine (Standart/Suit) atanir.
    private static async Task SeedRezervasyonIkiOdaTipindeAsync(
        StysAppDbContext dbContext,
        DateTime girisTarihi,
        DateTime cikisTarihi)
    {
        var ortaTarih = girisTarihi.AddDays(2);

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
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();

        var segment1 = new RezervasyonSegment
        {
            RezervasyonId = rezervasyon.Id,
            SegmentSirasi = 0,
            BaslangicTarihi = girisTarihi,
            BitisTarihi = ortaTarih
        };
        var segment2 = new RezervasyonSegment
        {
            RezervasyonId = rezervasyon.Id,
            SegmentSirasi = 1,
            BaslangicTarihi = ortaTarih,
            BitisTarihi = cikisTarihi
        };
        dbContext.RezervasyonSegmentleri.AddRange(segment1, segment2);
        await dbContext.SaveChangesAsync();

        dbContext.RezervasyonSegmentOdaAtamalari.AddRange(
            new RezervasyonSegmentOdaAtama
            {
                RezervasyonSegmentId = segment1.Id,
                OdaId = 100,
                AyrilanKisiSayisi = 2,
                OdaNoSnapshot = "101",
                BinaAdiSnapshot = "Bina-1",
                OdaTipiAdiSnapshot = "Standart",
                KapasiteSnapshot = 2
            },
            new RezervasyonSegmentOdaAtama
            {
                RezervasyonSegmentId = segment2.Id,
                OdaId = 101,
                AyrilanKisiSayisi = 2,
                OdaNoSnapshot = "201",
                BinaAdiSnapshot = "Bina-1",
                OdaTipiAdiSnapshot = "Suit",
                KapasiteSnapshot = 4
            });
        await dbContext.SaveChangesAsync();
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
