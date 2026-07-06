using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.GunlukGirisCikis.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class GunlukGirisCikisRaporServiceTests
{
    private static readonly DateTime SeciliGun = new(2026, 7, 10);

    // Secilen tarihte girisi olan rezervasyon "giris" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_SeciliGundeGirisiOlanRezervasyonGirisOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("giris", kayit.ListeDurumu);
        Assert.True(kayit.GirisYapacakMi);
    }

    // Secilen tarihte cikisi olan rezervasyon "cikis" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_SeciliGundeCikisiOlanRezervasyonCikisOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-3), cikisTarihi: SeciliGun, toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("cikis", kayit.ListeDurumu);
        Assert.True(kayit.CikisYapacakMi);
    }

    // Giristen sonra, cikistan once olan rezervasyon "devam-eden" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_GirisSonrasiCikisOncesiRezervasyonDevamEdenOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-2), cikisTarihi: SeciliGun.AddDays(2), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("devam-eden", kayit.ListeDurumu);
        Assert.True(kayit.DevamEdiyorMu);
    }

    // Cikis tarihi gecmis ama check-out tamamlanmamis rezervasyon "geciken-cikis" olarak gelir.
    [Fact]
    public async Task GetRaporAsync_CikisTarihiGecmisVeCheckOutTamamlanmamisRezervasyonGecikenCikisOlarakGelir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-5), cikisTarihi: SeciliGun.AddDays(-1), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("geciken-cikis", kayit.ListeDurumu);
        Assert.True(kayit.GecikenCikisMi);
    }

    // Iptal rezervasyon dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // listeTipi=girisler sadece girisleri getirir.
    [Fact]
    public async Task GetRaporAsync_GirislerFiltresiSadeceGirisleriGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GIRIS");
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-3), cikisTarihi: SeciliGun, toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-CIKIS");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "girisler");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-GIRIS", kayit.ReferansNo);
    }

    // listeTipi=cikislar sadece cikislari getirir.
    [Fact]
    public async Task GetRaporAsync_CikislarFiltresiSadeceCikislariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GIRIS");
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-3), cikisTarihi: SeciliGun, toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-CIKIS");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "cikislar");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-CIKIS", kayit.ReferansNo);
    }

    // listeTipi=devam-edenler sadece devam edenleri getirir.
    [Fact]
    public async Task GetRaporAsync_DevamEdenlerFiltresiSadeceDevamEdenleriGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GIRIS");
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-2), cikisTarihi: SeciliGun.AddDays(2), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-DEVAM");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "devam-edenler");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-DEVAM", kayit.ReferansNo);
    }

    // listeTipi=geciken-cikislar sadece geciken cikislari getirir.
    [Fact]
    public async Task GetRaporAsync_GecikenCikislarFiltresiSadeceGecikenCikislariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GIRIS");
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-5), cikisTarihi: SeciliGun.AddDays(-1), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GECIKEN");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "geciken-cikislar");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("REF-GECIKEN", kayit.ReferansNo);
    }

    // Ozet, filtre sonrasi listeyle uyumlu olur.
    [Fact]
    public async Task GetRaporAsync_OzetFiltreSonrasiListeyleUyumluOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GIRIS");
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-3), cikisTarihi: SeciliGun, toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-CIKIS");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "girisler");

        Assert.Equal(1, rapor.Ozet.ToplamRezervasyonSayisi);
        Assert.Equal(1, rapor.Ozet.GirisSayisi);
        Assert.Equal(0, rapor.Ozet.CikisSayisi);
    }

    // Silinmis odeme kalan tutar hesabina dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_SilinmisOdemeKalanTutarHesabinaDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var silinecekOdeme = new RezervasyonOdeme
        {
            RezervasyonId = rezervasyon.Id,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = 1000m,
            ParaBirimi = "TRY",
            OdemeTipi = OdemeTipleri.Nakit
        };
        dbContext.RezervasyonOdemeler.Add(silinecekOdeme);
        await dbContext.SaveChangesAsync();
        dbContext.RezervasyonOdemeler.Remove(silinecekOdeme);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(0m, kayit.OdenenTutar);
        Assert.Equal(1000m, kayit.KalanTutar);
    }

    // Silinmis oda atamasi oda listesine dahil edilmez.
    [Fact]
    public async Task GetRaporAsync_SilinmisOdaAtamasiOdaListesineDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var segment = await dbContext.RezervasyonSegmentleri.FirstAsync(s => s.RezervasyonId == rezervasyon.Id);
        var silinecekAtama = new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = segment.Id,
            OdaId = 200,
            AyrilanKisiSayisi = 2,
            OdaNoSnapshot = "202",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        };
        dbContext.RezervasyonSegmentOdaAtamalari.Add(silinecekAtama);
        await dbContext.SaveChangesAsync();
        dbContext.RezervasyonSegmentOdaAtamalari.Remove(silinecekAtama);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, SeciliGun, "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.DoesNotContain("202", kayit.OdaNolari);
        Assert.Contains("101", kayit.OdaNolari);
    }

    // Yetkisiz tesis icin 403 doner.
    [Fact]
    public async Task GetRaporAsync_YetkisizTesisIcin403Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = new GunlukGirisCikisRaporService(
            dbContext,
            new FakeUserAccessScopeService(scoped: true, izinliTesisIds: [999]),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetRaporAsync(1, SeciliGun, "tumu"));

        Assert.Equal(403, exception.ErrorCode);
    }

    private static GunlukGirisCikisRaporService CreateService(StysAppDbContext dbContext)
    {
        return new GunlukGirisCikisRaporService(
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

    private static async Task SeedTesisAsync(StysAppDbContext dbContext)
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

        await dbContext.SaveChangesAsync();
    }

    private static async Task<Rezervasyon> SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        decimal toplamUcret,
        string rezervasyonDurumu,
        string? referansNo = null)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = referansNo ?? $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = 2,
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

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            RezervasyonSegmentId = segment.Id,
            OdaId = 100,
            AyrilanKisiSayisi = 2,
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
