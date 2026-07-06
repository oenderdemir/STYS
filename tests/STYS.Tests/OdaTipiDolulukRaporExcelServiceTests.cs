using ClosedXML.Excel;
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

namespace STYS.Tests;

public class OdaTipiDolulukRaporExcelServiceTests
{
    private static readonly DateTime Baslangic = new(2026, 7, 1);
    private static readonly DateTime Bitis = new(2026, 7, 7);

    // Excel byte[] bos donmemeli.
    [Fact]
    public async Task OlusturAsync_ExcelBinaryBosDonmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        Assert.NotEmpty(bytes);
    }

    // Workbook icinde Ozet, Oda Tipi Doluluk ve Oda Detaylari sheetleri olusmali.
    [Fact]
    public async Task OlusturAsync_UcSheetOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Oda Tipi Doluluk"));
        Assert.True(workbook.Worksheets.Contains("Oda Detayları"));
    }

    // Oda Tipi Doluluk sheet headerlari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_OdaTipiDolulukSheetHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Oda Tipi Doluluk");

        Assert.Equal("Oda Tipi", ws.Cell(1, 1).GetString());
        Assert.Equal("Doluluk Oranı", ws.Cell(1, 7).GetString());
        Assert.Equal("Müsaitlik Oranı", ws.Cell(1, 8).GetString());
        Assert.Equal("Kişi-Gece", ws.Cell(1, 11).GetString());
    }

    // Oda Detaylari sheet headerlari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_OdaDetaylariSheetHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Oda Detayları");

        Assert.Equal("Oda Tipi", ws.Cell(1, 1).GetString());
        Assert.Equal("Oda No", ws.Cell(1, 2).GetString());
        Assert.Equal("Doluluk Oranı", ws.Cell(1, 8).GetString());
    }

    // Yuzde formati 0.00"%" olarak korunmali.
    [Fact]
    public async Task OlusturAsync_YuzdeFormatiKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, odaTipiId: 20);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Oda Tipi Doluluk");

        Assert.Equal("0.00\"%\"", ws.Cell(2, 7).Style.NumberFormat.Format);
    }

    // Ozet sheetinde Kisi-Gece notu bulunmali.
    [Fact]
    public async Task OlusturAsync_OzetSheetindeKisiGeceNotuBulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Özet");

        Assert.Contains(
            ws.CellsUsed().Select(c => c.GetString()),
            x => x.Contains("Kişi-Gece değeri oda tipi kullanım yoğunluğu için yaklaşık metrik olarak hesaplanır"));
    }

    // AutoFilter olusmali.
    [Fact]
    public async Task OlusturAsync_AutoFilterOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var dolulukWs = workbook.Worksheet("Oda Tipi Doluluk");
        var detayWs = workbook.Worksheet("Oda Detayları");

        Assert.True(dolulukWs.AutoFilter.IsEnabled);
        Assert.True(detayWs.AutoFilter.IsEnabled);
    }

    private static OdaTipiDolulukRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new OdaTipiDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OdaTipiDolulukRaporExcelService(raporService);
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

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi)
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
            AyrilanKisiSayisi = 2,
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
