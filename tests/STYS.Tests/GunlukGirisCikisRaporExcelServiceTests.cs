using ClosedXML.Excel;
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

namespace STYS.Tests;

public class GunlukGirisCikisRaporExcelServiceTests
{
    private static readonly DateTime SeciliGun = new(2026, 7, 10);

    // Excel byte[] bos donmemeli.
    [Fact]
    public async Task OlusturAsync_ExcelBinaryBosDonmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, SeciliGun, "tumu");

        Assert.NotEmpty(bytes);
    }

    // Workbook icinde Ozet ve Liste sheetleri olusmali.
    [Fact]
    public async Task OlusturAsync_OzetVeListeSheetleriOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, SeciliGun, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Liste"));
    }

    // Liste sheet header'lari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_ListeSheetHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, SeciliGun, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Liste");

        Assert.Equal("Liste Durumu", ws.Cell(1, 1).GetString());
        Assert.Equal("Kalan Tutar", ws.Cell(1, 12).GetString());
        Assert.Equal("Para Birimi", ws.Cell(1, 13).GetString());
        Assert.Equal("Açıklama", ws.Cell(1, 14).GetString());
    }

    // Geciken cikis satiri renklendirilmeli.
    [Fact]
    public async Task OlusturAsync_GecikenCikisSatiriRenklendirilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun.AddDays(-5), cikisTarihi: SeciliGun.AddDays(-1), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-GECIKEN");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, SeciliGun, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Liste");
        var satir = ws.RowsUsed().Skip(1).Single(r => r.Cell(2).GetString() == "REF-GECIKEN");

        Assert.Equal(XLColor.FromHtml("#F8CBAD"), satir.Cell(1).Style.Fill.BackgroundColor);
    }

    // Kalan tutar para formatiyla yazilmali.
    [Fact]
    public async Task OlusturAsync_KalanTutarParaFormatiyleYazilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTesisAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, girisTarihi: SeciliGun, cikisTarihi: SeciliGun.AddDays(3), toplamUcret: 1000m, rezervasyonDurumu: RezervasyonDurumlari.Onayli, referansNo: "REF-KALAN");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, SeciliGun, "tumu");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Liste");
        var satir = ws.RowsUsed().Skip(1).Single(r => r.Cell(2).GetString() == "REF-KALAN");

        Assert.Equal(1000m, satir.Cell(12).GetValue<decimal>());
        Assert.Contains("₺", satir.Cell(12).Style.NumberFormat.Format);
    }

    private static GunlukGirisCikisRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new GunlukGirisCikisRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new GunlukGirisCikisRaporExcelService(raporService);
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

    private static async Task SeedRezervasyonAsync(
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
