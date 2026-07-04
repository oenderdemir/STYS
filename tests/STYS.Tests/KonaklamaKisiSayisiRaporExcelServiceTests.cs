using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using C = DocumentFormat.OpenXml.Drawing.Charts;
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

public class KonaklamaKisiSayisiRaporExcelServiceTests
{
    // Excel binary uretimi bos donmemeli.
    [Fact]
    public async Task OlusturAsync_ExcelBinaryBosDonmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 5, 2025, 2026);

        Assert.NotEmpty(bytes);
    }

    // Workbook "Konaklama Kisi Sayisi" sheet'ini icermeli, baslik ve header'lar dogru olusmali.
    [Fact]
    public async Task OlusturAsync_SheetBaslikVeHeaderlarDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 5, 2025, 2026);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = Assert.Single(workbook.Worksheets);
        Assert.Equal("Konaklama Kişi Sayısı", ws.Name);

        var tumHucreler = ws.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("2025-2026 MAYIS AYI KONAKLAYAN KİŞİ SAYISI", tumHucreler);
        Assert.Contains("101 NOLU ODA", tumHucreler);
        Assert.Contains("TOPLAM SAYI", tumHucreler);
    }

    // Ornek veriyle 2026 satirinin toplami beklenen degeri vermeli.
    [Fact]
    public async Task OlusturAsync_2026ToplamiBeklenenDegeriVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 5, 10), cikisTarihi: new DateTime(2026, 5, 12), ayrilanKisiSayisi: 2);
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: new DateTime(2026, 5, 10), cikisTarihi: new DateTime(2026, 5, 12), ayrilanKisiSayisi: 3);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 5, 2026, 2026);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheets.Single();

        // Header row 4, ilk yil satiri row 5; kolon 1=YIL, 2=101, 3=102, 4=TOPLAM SAYI.
        Assert.Equal(2026, ws.Cell(5, 1).GetValue<int>());
        Assert.Equal(5, ws.Cell(5, 4).GetValue<int>());
    }

    // Excel workbook'unda oda bazli grafik gomulu olmali; TOPLAM SAYI kolonu grafigin veri araligina dahil edilmemeli.
    [Fact]
    public async Task OlusturAsync_GrafikIcerenExcelUretir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 5, 10), cikisTarihi: new DateTime(2026, 5, 12), ayrilanKisiSayisi: 2);
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: new DateTime(2026, 5, 10), cikisTarihi: new DateTime(2026, 5, 12), ayrilanKisiSayisi: 3);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 5, 2026, 2026);

        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
        var drawingsPart = worksheetPart.GetPartsOfType<DrawingsPart>().Single();
        var chartParts = drawingsPart.ChartParts.ToList();

        Assert.Equal(2, chartParts.Count);

        var odaBazliChart = chartParts
            .Select(x => x.ChartSpace.Descendants<C.Title>().FirstOrDefault())
            .First(t => t != null && t.InnerText.Contains("Oda Bazlı Konaklayan Kişi Sayısı Karşılaştırması"))!
            .Ancestors<C.ChartSpace>()
            .First();

        var seriDegerFormulleri = odaBazliChart.Descendants<C.BarChartSeries>()
            .Select(s => s.Descendants<C.NumberReference>().Single().Descendants<C.Formula>().Single().Text)
            .ToList();

        Assert.NotEmpty(seriDegerFormulleri);
        Assert.All(seriDegerFormulleri, f => Assert.DoesNotContain("$D$", f));
    }

    private static KonaklamaKisiSayisiRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new KonaklamaKisiSayisiRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new KonaklamaKisiSayisiRaporExcelService(raporService);
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
        int ayrilanKisiSayisi)
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
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
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
