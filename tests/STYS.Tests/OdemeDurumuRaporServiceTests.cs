using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Raporlar.OdemeDurumu.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class OdemeDurumuRaporServiceTests
{
    // Odemesi olmayan rezervasyon "odemesi-yok" olarak siniflandirilmali.
    [Fact]
    public async Task GetRaporAsync_OdemeYapilmamisRezervasyonOdemesiYokOlarakSiniflandirilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        var rezervasyon = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("odemesi-yok", rezervasyon.OdemeDurumu);
        Assert.Equal(1000m, rezervasyon.KalanTutar);
        Assert.True(rezervasyon.BorcluMu);
    }

    // Kismi odeme yapilan rezervasyon "kismi-odendi" olarak siniflandirilmali ve kalan tutar dogru hesaplanmali.
    [Fact]
    public async Task GetRaporAsync_KismiOdemeYapilanRezervasyonKismiOdendiOlarakSiniflandirilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedOdemeAsync(dbContext, rezervasyon.Id, 400m);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("kismi-odendi", kayit.OdemeDurumu);
        Assert.Equal(600m, kayit.KalanTutar);
        Assert.True(kayit.BorcluMu);
    }

    // Tam odeme yapilan rezervasyon "tamamen-odendi" olarak siniflandirilmali ve kalan tutar 0 olmali.
    [Fact]
    public async Task GetRaporAsync_TamOdemeYapilanRezervasyonTamamenOdendiOlarakSiniflandirilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedOdemeAsync(dbContext, rezervasyon.Id, 1000m);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal("tamamen-odendi", kayit.OdemeDurumu);
        Assert.Equal(0m, kayit.KalanTutar);
        Assert.False(kayit.BorcluMu);
    }

    // Fazla odeme (toplam ucretten fazla) yapilirsa kalan tutar 0'a sabitlenmeli ve tamamen-odendi kabul edilmeli.
    [Fact]
    public async Task GetRaporAsync_FazlaOdemeVarsaKalanSifiraSabitlenirVeTamamenOdendiKabulEdilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedOdemeAsync(dbContext, rezervasyon.Id, 1500m);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(0m, kayit.KalanTutar);
        Assert.Equal("tamamen-odendi", kayit.OdemeDurumu);
        Assert.False(kayit.BorcluMu);
    }

    // Iptal edilen rezervasyonlar rapora dahil edilmemeli.
    [Fact]
    public async Task GetRaporAsync_IptalRezervasyonRaporaDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // Tarih araligiyla cakismayan rezervasyonlar rapora dahil edilmemeli.
    [Fact]
    public async Task GetRaporAsync_TarihAraligiIleCakismayanRezervasyonlarHaricTutulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 5, 1), cikisTarihi: new DateTime(2026, 5, 5), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        Assert.Empty(rapor.Rezervasyonlar);
    }

    // CheckOutTamamlandi durumundaki ve borcu olan rezervasyon cikis-yapmis-borclu olarak isaretlenmeli.
    [Fact]
    public async Task GetRaporAsync_CheckOutTamamlandiVeBorcluRezervasyonCikisYapmisBorcluOlarakIsaretlenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.True(kayit.CikisYapmisMi);
        Assert.True(kayit.CikisYapmisBorcluMu);
    }

    // odemeDurumu=borclu filtresi yalnizca kalan tutari pozitif olan rezervasyonlari getirmeli.
    [Fact]
    public async Task GetRaporAsync_BorcluFiltresiSadeceKalanTutariPozitifOlanlariGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var odenmis = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedOdemeAsync(dbContext, odenmis.Id, 1000m);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 15), cikisTarihi: new DateTime(2026, 6, 18), toplamUcret: 500m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "borclu");

        var kayit = Assert.Single(rapor.Rezervasyonlar);
        Assert.Equal(500m, kayit.ToplamUcret);
    }

    // Ozet toplamlari, filtrelenmis rezervasyon listesinin toplamlarina esit olmali.
    [Fact]
    public async Task GetRaporAsync_OzetToplamlariFiltrelenmisListeyeEsitOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var r1 = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedOdemeAsync(dbContext, r1.Id, 300m);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 15), cikisTarihi: new DateTime(2026, 6, 18), toplamUcret: 500m, rezervasyonDurumu: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        Assert.Equal(2, rapor.Ozet.ToplamRezervasyonSayisi);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.ToplamUcret), rapor.Ozet.ToplamUcret);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.OdenenTutar), rapor.Ozet.ToplamOdenenTutar);
        Assert.Equal(rapor.Rezervasyonlar.Sum(x => x.KalanTutar), rapor.Ozet.ToplamKalanTutar);
    }

    // Siralama: cikis-yapmis-borclu once, sonra kalan tutara gore azalan, sonra giris tarihine gore artan olmali.
    [Fact]
    public async Task GetRaporAsync_SiralamaCikisYapmisBorcluKalanTutarVeGirisTarihineGoreYapilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 20), cikisTarihi: new DateTime(2026, 6, 22), toplamUcret: 300m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-A");
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 5), cikisTarihi: new DateTime(2026, 6, 8), toplamUcret: 900m, rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi, referansNo: "REF-B");
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 1), cikisTarihi: new DateTime(2026, 6, 3), toplamUcret: 500m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-C");

        var service = CreateService(dbContext);
        var rapor = await service.GetRaporAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        Assert.Equal(["REF-B", "REF-C", "REF-A"], rapor.Rezervasyonlar.Select(x => x.ReferansNo));
    }

    // Gecersiz tarih araligi (baslangic > bitis) BaseException 400 firlatmali.
    [Fact]
    public async Task GetRaporAsync_BaslangicBitistenBuyukIseBaseExceptionFirlatir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(
            () => service.GetRaporAsync(1, new DateTime(2026, 6, 30), new DateTime(2026, 6, 1), "tumu"));

        Assert.Equal(400, exception.ErrorCode);
    }

    private static OdemeDurumuRaporService CreateService(StysAppDbContext dbContext)
    {
        return new OdemeDurumuRaporService(
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

    private static async Task SeedOdemeAsync(StysAppDbContext dbContext, int rezervasyonId, decimal tutar)
    {
        dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
        {
            RezervasyonId = rezervasyonId,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = tutar,
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
