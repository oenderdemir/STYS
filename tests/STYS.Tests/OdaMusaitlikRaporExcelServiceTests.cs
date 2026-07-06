using ClosedXML.Excel;
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

namespace STYS.Tests;

public class OdaMusaitlikRaporExcelServiceTests
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
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        Assert.NotEmpty(bytes);
    }

    // Workbook icinde Ozet, Musaitlik Matrisi ve Oda Listesi sheetleri olusmali.
    [Fact]
    public async Task OlusturAsync_UcSheetOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Müsaitlik Matrisi"));
        Assert.True(workbook.Worksheets.Contains("Oda Listesi"));
    }

    // Musaitlik Matrisi headerlari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_MusaitlikMatrisiHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Müsaitlik Matrisi");

        Assert.Equal("Oda No", ws.Cell(1, 1).GetString());
        Assert.Equal("Bina", ws.Cell(1, 2).GetString());
        Assert.Equal("Oda Tipi", ws.Cell(1, 3).GetString());
        Assert.Equal("Kapasite", ws.Cell(1, 4).GetString());
        Assert.Equal("01.07", ws.Cell(1, 5).GetString());
        Assert.Equal("07.07", ws.Cell(1, 11).GetString());
    }

    // BOS ve DOLU hucreleri dogru yazilmali.
    [Fact]
    public async Task OlusturAsync_BosVeDoluHucreleriDogruYazilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Müsaitlik Matrisi");

        var doluOdaSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "101");
        var bosOdaSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "102");

        Assert.Equal("DOLU", doluOdaSatir.Cell(5).GetString());
        Assert.Equal("BOŞ", bosOdaSatir.Cell(5).GetString());
    }

    // BOS/DOLU hucre renkleri dogru uygulanmali.
    [Fact]
    public async Task OlusturAsync_BosVeDoluHucreRenkleriDogruUygulanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: Baslangic, cikisTarihi: Bitis.AddDays(1));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Müsaitlik Matrisi");

        var doluOdaSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "101");
        var bosOdaSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "102");

        Assert.Equal(XLColor.FromHtml("#F8CBAD"), doluOdaSatir.Cell(5).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#C6E0B4"), bosOdaSatir.Cell(5).Style.Fill.BackgroundColor);
    }

    // Oda Listesi sheetinde yuzde formati olusmali.
    [Fact]
    public async Task OlusturAsync_OdaListesiSheetindeYuzdeFormatiOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, Baslangic, Bitis, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Oda Listesi");

        Assert.Equal("Müsaitlik Oranı", ws.Cell(1, 9).GetString());
        Assert.Contains("%", ws.Cell(2, 9).Style.NumberFormat.Format);
    }

    private static OdaMusaitlikRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new OdaMusaitlikRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OdaMusaitlikRaporExcelService(raporService);
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
