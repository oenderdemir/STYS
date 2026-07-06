using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.RezervasyonDurumDagilimi.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class RezervasyonDurumDagilimiRaporServiceTests
{
    private static readonly DateTime RaporBaslangic = new(2026, 7, 1);
    private static readonly DateTime RaporBitis = new(2026, 7, 31);

    // Iptal rezervasyon rapora dahil edilir.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonRaporaDahilEdilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(RezervasyonDurumlari.Iptal, rezervasyon.RezervasyonDurumu);
    }

    // Soft-delete rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_SilinmisRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        rezervasyon.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Aktif olmayan rezervasyon rapora dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_AktifOlmayanRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        var (rezervasyon, _, _) = await SeedRezervasyonDetayliAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        rezervasyon.AktifMi = false;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Durum bazli sayilar dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_DurumBazliSayilarDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.Taslak);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 7), rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 9), cikisTarihi: new DateTime(2026, 7, 11), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Equal(1, rapor.Ozet.TaslakSayisi);
        Assert.Equal(1, rapor.Ozet.OnayliSayisi);
        Assert.Equal(1, rapor.Ozet.IptalSayisi);
        Assert.Equal(3, rapor.Ozet.ToplamRezervasyonSayisi);
    }

    // Iptal orani dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_IptalOraniDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.Iptal);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 7), rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 9), cikisTarihi: new DateTime(2026, 7, 11), rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 13), cikisTarihi: new DateTime(2026, 7, 15), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Equal(25m, rapor.Ozet.IptalOrani);
    }

    // Gerceklesme orani CheckInTamamlandi + CheckOutTamamlandi uzerinden dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_GerceklesmeOraniCheckInVeCheckOutUzerindenHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.CheckInTamamlandi);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 7), rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 9), cikisTarihi: new DateTime(2026, 7, 11), rezervasyonDurumu: RezervasyonDurumlari.Taslak);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 13), cikisTarihi: new DateTime(2026, 7, 15), rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Equal(2, rapor.Ozet.GerceklesenRezervasyonSayisi);
        Assert.Equal(50m, rapor.Ozet.GerceklesmeOrani);
    }

    // Durum filtresi sadece ilgili durumu getirir.
    [Fact]
    public async Task GetRaporAsync_DurumFiltresiSadeceIlgiliDurumuGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.Iptal, referansNo: "REF-IPTAL");
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 7), rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-ONAYLI");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis, durum: "iptal");

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-IPTAL", rezervasyon.ReferansNo);
    }

    // odaTipiId filtresi sadece ilgili oda tipine atanmis rezervasyonlari getirir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdFiltresiSadeceIlgiliOdaTipiniGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), referansNo: "REF-STANDART");
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), referansNo: "REF-SUIT");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis, odaTipiId: 21);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-SUIT", rezervasyon.ReferansNo);
    }

    // odaTipiId filtresinde DTO OdaTipleri sadece filtrelenen oda tipini gosterir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiIdFiltresindeOdaTipleriSadeceFiltrelenenOdaTipiniGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonIkiOdaTipindeAsync(dbContext, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis, odaTipiId: 20);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(["Standart"], rezervasyon.OdaTipleri);
    }

    // Cikis gunu geceye dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_CikisGunuGeceyeDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 10), cikisTarihi: new DateTime(2026, 7, 11));

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(1, rezervasyon.GeceSayisi);
    }

    // Oda tipi dagilimi dogru hesaplanir.
    [Fact]
    public async Task GetRaporAsync_OdaTipiDagilimiDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 7), rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        var odaTipi = Assert.Single(rapor.OdaTipleri, x => x.OdaTipiId == 20);
        Assert.Equal(2, odaTipi.RezervasyonSayisi);
        Assert.Equal(1, odaTipi.IptalSayisi);
        Assert.Equal(1, odaTipi.GerceklesenSayisi);
    }

    // Genel ozet rezervasyon listesiyle uyumludur.
    [Fact]
    public async Task GetRaporAsync_GenelOzetRezervasyonListesiyleUyumludur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), kisiSayisi: 2);
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: new DateTime(2026, 7, 5), cikisTarihi: new DateTime(2026, 7, 10), kisiSayisi: 3, rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, RaporBaslangic, RaporBitis);

        Assert.Equal(2, rapor.Ozet.ToplamRezervasyonSayisi);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.KisiSayisi), rapor.Ozet.ToplamKisiSayisi);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.GeceSayisi), rapor.Ozet.ToplamGeceSayisi);
    }

    // Yetkisiz tesis icin 403 doner.
    [Fact]
    public async Task GetRaporAsync_YetkisizTesisIcin403Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = new RezervasyonDurumDagilimiRaporService(
            dbContext,
            new FakeUserAccessScopeService(scoped: true, izinliTesisIds: [999]),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetRaporAsync(1, RaporBaslangic, RaporBitis));

        Assert.Equal(403, exception.ErrorCode);
    }

    private static RezervasyonDurumDagilimiRaporService CreateService(StysAppDbContext dbContext)
    {
        return new RezervasyonDurumDagilimiRaporService(
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
        int kisiSayisi = 2)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = referansNo ?? $"REF-{Guid.NewGuid():N}"[..12],
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

    // Tek rezervasyon, secilen aralikla cakisan iki ayri segmentte iki farkli oda tipine (Standart/Suit) atanir.
    private static async Task SeedRezervasyonIkiOdaTipindeAsync(
        StysAppDbContext dbContext,
        DateTime girisTarihi,
        DateTime cikisTarihi)
    {
        var ortaTarih = girisTarihi.AddDays(1);

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
