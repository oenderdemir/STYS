using ClosedXML.Excel;
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

namespace STYS.Tests;

public class OdemeDurumuRaporExcelServiceTests
{
    // Excel binary uretimi bos donmemeli.
    [Fact]
    public async Task OlusturAsync_ExcelBinaryBosDonmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        Assert.NotEmpty(bytes);
    }

    // Workbook iki sheet icermeli: Ozet ve Rezervasyonlar.
    [Fact]
    public async Task OlusturAsync_OzetVeRezervasyonlarSheetleriOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Rezervasyonlar"));
    }

    // Ozet sayfasi tesis adini ve toplam rezervasyon sayisini icermeli.
    [Fact]
    public async Task OlusturAsync_OzetSayfasiTesisAdiVeToplamlariIcerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Özet");
        var tumHucreler = ws.CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Test Tesis", tumHucreler);
        var etiketHucresi = ws.CellsUsed().Single(c => c.GetString() == "Toplam Rezervasyon Sayısı");
        var degerHucresi = ws.Cell(etiketHucresi.Address.RowNumber, etiketHucresi.Address.ColumnNumber + 1);
        Assert.Equal(1, degerHucresi.GetValue<int>());
    }

    // Rezervasyonlar sayfasi 15 kolonluk header satirini icermeli (Para Birimi kolonu dahil).
    [Fact]
    public async Task OlusturAsync_RezervasyonlarSayfasiHeaderKolonlariniIcerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        Assert.Equal("Referans No", ws.Cell(1, 1).GetString());
        Assert.Equal("Kalan Tutar", ws.Cell(1, 11).GetString());
        Assert.Equal("Para Birimi", ws.Cell(1, 12).GetString());
        Assert.Equal("Ödeme Durumu", ws.Cell(1, 13).GetString());
        Assert.Equal("Çıkış Yapmış mı", ws.Cell(1, 15).GetString());
    }

    // Rezervasyon satirlarindaki degerler (referans no, tutarlar) dogru yazilmali.
    [Fact]
    public async Task OlusturAsync_RezervasyonSatiriDegerleriDogruYazilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, referansNo: "REF-EXCEL-1");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        Assert.Equal("REF-EXCEL-1", ws.Cell(2, 1).GetString());
        Assert.Equal(1000m, ws.Cell(2, 9).GetValue<decimal>());
        Assert.Equal(1000m, ws.Cell(2, 11).GetValue<decimal>());
    }

    // Para Birimi kolonu rezervasyonun para birimi kodunu icermeli ve tutar kolonlari para birimi sembolu ile formatlanmali.
    [Fact]
    public async Task OlusturAsync_ParaBirimiKolonuVeTutarFormatiDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, referansNo: "REF-EXCEL-2");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        Assert.Equal("TRY", ws.Cell(2, 12).GetString());
        Assert.Contains("₺", ws.Cell(2, 9).Style.NumberFormat.Format);
        Assert.Contains("₺", ws.Cell(2, 10).Style.NumberFormat.Format);
        Assert.Contains("₺", ws.Cell(2, 11).Style.NumberFormat.Format);
    }

    // Borclu ve cikis yapmis borclu satirlarin renklendirmesi Para Birimi kolonu eklendikten sonra da dogru calismali.
    // REF-BORCLU henuz cikis yapmamis (cikis tarihi ileride) ama borclu; REF-CIKIS-BORCLU gecmis bir cikis tarihine sahip ve borclu.
    [Fact]
    public async Task OlusturAsync_BorcluVeCikisYapmisBorcluSatirRenklendirmesiCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var ileriTarih = DateTime.Now.Date.AddMonths(2);
        var gecmisTarih = DateTime.Now.Date.AddDays(-5);
        await SeedRezervasyonAsync(dbContext, girisTarihi: ileriTarih, cikisTarihi: ileriTarih.AddDays(3), toplamUcret: 1000m, referansNo: "REF-BORCLU", rezervasyonDurumu: RezervasyonDurumlari.Onayli);
        await SeedRezervasyonAsync(dbContext, girisTarihi: gecmisTarih.AddDays(-3), cikisTarihi: gecmisTarih, toplamUcret: 500m, referansNo: "REF-CIKIS-BORCLU", rezervasyonDurumu: RezervasyonDurumlari.CheckOutTamamlandi);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, gecmisTarih.AddDays(-10), ileriTarih.AddDays(10), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        var cikisBorcluSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-CIKIS-BORCLU");
        var borcluSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-BORCLU");

        Assert.Equal(XLColor.FromHtml("#F8CBAD"), cikisBorcluSatir.Cell(1).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#FFF2CC"), borcluSatir.Cell(1).Style.Fill.BackgroundColor);
    }

    // Silinmis (soft-delete) odeme kaydi borc hesabina dahil edilmemeli; Excel'de eski kalan tutar gorunmeli.
    [Fact]
    public async Task OlusturAsync_SilinmisOdemeKaydiBorcHesabinaDahilEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        var rezervasyon = await SeedRezervasyonAsync(dbContext, girisTarihi: new DateTime(2026, 6, 10), cikisTarihi: new DateTime(2026, 6, 13), toplamUcret: 1000m, referansNo: "REF-SILINMIS-ODEME");

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

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        Assert.Equal(1000m, ws.Cell(2, 11).GetValue<decimal>());
    }

    private static OdemeDurumuRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new OdemeDurumuRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OdemeDurumuRaporExcelService(raporService);
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
        string? referansNo = null,
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli)
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
