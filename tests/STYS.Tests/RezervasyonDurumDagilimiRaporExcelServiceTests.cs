using ClosedXML.Excel;
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

namespace STYS.Tests;

public class RezervasyonDurumDagilimiRaporExcelServiceTests
{
    private static readonly DateTime RaporBaslangic = new(2026, 7, 1);
    private static readonly DateTime RaporBitis = new(2026, 7, 31);

    // Excel byte[] bos donmemeli.
    [Fact]
    public async Task OlusturAsync_ExcelBinaryBosDonmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        Assert.NotEmpty(bytes);
    }

    // Workbook icinde Ozet, Durum Dagilimi, Oda Tipi Dagilimi ve Rezervasyonlar sheetleri olusmali.
    [Fact]
    public async Task OlusturAsync_DortSheetOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(4, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Durum Dağılımı"));
        Assert.True(workbook.Worksheets.Contains("Oda Tipi Dağılımı"));
        Assert.True(workbook.Worksheets.Contains("Rezervasyonlar"));
    }

    // Durum Dagilimi sheet headerlari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_DurumDagilimiSheetHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Durum Dağılımı");

        Assert.Equal("Durum", ws.Cell(1, 1).GetString());
        Assert.Equal("Rezervasyon Sayısı", ws.Cell(1, 2).GetString());
        Assert.Equal("Oran", ws.Cell(1, 5).GetString());
    }

    // Oran formati 0.00"%" olarak korunmali.
    [Fact]
    public async Task OlusturAsync_OranFormatiKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Durum Dağılımı");

        Assert.Equal("0.00\"%\"", ws.Cell(2, 5).Style.NumberFormat.Format);
    }

    // Iptal satir rengi uygulanmali.
    [Fact]
    public async Task OlusturAsync_IptalSatirRengiUygulanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), rezervasyonDurumu: RezervasyonDurumlari.Iptal, referansNo: "REF-IPTAL");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");
        var satir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-IPTAL");

        Assert.Equal(XLColor.FromHtml("#FCE4E4"), satir.Cell(1).Style.Fill.BackgroundColor);
    }

    // AutoFilter olusmali.
    [Fact]
    public async Task OlusturAsync_AutoFilterOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(workbook.Worksheet("Durum Dağılımı").AutoFilter.IsEnabled);
        Assert.True(workbook.Worksheet("Oda Tipi Dağılımı").AutoFilter.IsEnabled);
        Assert.True(workbook.Worksheet("Rezervasyonlar").AutoFilter.IsEnabled);
    }

    private static RezervasyonDurumDagilimiRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new RezervasyonDurumDagilimiRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new RezervasyonDurumDagilimiRaporExcelService(raporService);
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
        DateTime cikisTarihi,
        string rezervasyonDurumu = RezervasyonDurumlari.Onayli,
        string? referansNo = null)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = referansNo ?? $"REF-{Guid.NewGuid():N}"[..12],
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
