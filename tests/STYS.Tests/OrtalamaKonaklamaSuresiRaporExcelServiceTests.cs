using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Tests;

public class OrtalamaKonaklamaSuresiRaporExcelServiceTests
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

    // Workbook icinde Ozet, Oda Tipi Ozeti ve Rezervasyonlar sheetleri olusmali.
    [Fact]
    public async Task OlusturAsync_UcSheetOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(3, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Özet"));
        Assert.True(workbook.Worksheets.Contains("Oda Tipi Özeti"));
        Assert.True(workbook.Worksheets.Contains("Rezervasyonlar"));
    }

    // Rezervasyonlar sheet headerlari dogru olusmali.
    [Fact]
    public async Task OlusturAsync_RezervasyonlarSheetHeaderlariDogruOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        Assert.Equal("Referans No", ws.Cell(1, 1).GetString());
        Assert.Equal("Gece Sayısı", ws.Cell(1, 5).GetString());
        Assert.Equal("Konaklama Grubu", ws.Cell(1, 10).GetString());
    }

    // Ortalama/gece decimal formati dogru uygulanmali.
    [Fact]
    public async Task OlusturAsync_OrtalamaGeceDecimalFormatiDogruUygulanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3));

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Oda Tipi Özeti");

        Assert.Equal("0.00", ws.Cell(2, 5).Style.NumberFormat.Format);
    }

    // Kisa/orta/uzun konaklama satir renkleri uygulanmali.
    [Fact]
    public async Task OlusturAsync_KonaklamaGrubuSatirRenkleriUygulanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(dbContext, odaId: 100, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), referansNo: "REF-KISA");
        await SeedRezervasyonAsync(dbContext, odaId: 101, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 15), referansNo: "REF-UZUN");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");

        var kisaSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-KISA");
        var uzunSatir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-UZUN");

        Assert.Equal(XLColor.FromHtml("#E2EFDA"), kisaSatir.Cell(1).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#FCE4D6"), uzunSatir.Cell(1).Style.Fill.BackgroundColor);
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
        var odaTipiWs = workbook.Worksheet("Oda Tipi Özeti");
        var rezervasyonlarWs = workbook.Worksheet("Rezervasyonlar");

        Assert.True(odaTipiWs.AutoFilter.IsEnabled);
        Assert.True(rezervasyonlarWs.AutoFilter.IsEnabled);
    }

    // odaTipiId filtresiyle Excel uretildiginde Rezervasyonlar sheetindeki Oda Tipi(leri) kolonu
    // sadece filtrelenen oda tipini gostermeli (rezervasyon iki farkli oda tipine gecmis olsa bile).
    [Fact]
    public async Task OlusturAsync_OdaTipiIdFiltresiyleRezervasyonlarSheetiOdaTipleriKolonuSadeceFiltrelenenOdaTipiniGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonIkiOdaTipindeAsync(dbContext, girisTarihi: new DateTime(2026, 7, 1), cikisTarihi: new DateTime(2026, 7, 3), referansNo: "REF-IKI-TIP");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, RaporBaslangic, RaporBitis, odaTipiId: 20);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("Rezervasyonlar");
        var satir = ws.RowsUsed().Skip(1).Single(r => r.Cell(1).GetString() == "REF-IKI-TIP");

        Assert.Equal("Standart", satir.Cell(8).GetString());
    }

    private static OrtalamaKonaklamaSuresiRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new OrtalamaKonaklamaSuresiRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OrtalamaKonaklamaSuresiRaporExcelService(raporService);
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
        dbContext.Odalar.Add(new Oda { Id = 101, OdaNo = "102", BinaId = 10, TesisOdaTipiId = 20, KatNo = 1, AktifMi = true });
        dbContext.Odalar.Add(new Oda { Id = 102, OdaNo = "201", BinaId = 10, TesisOdaTipiId = 21, KatNo = 2, AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
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

    // Tek rezervasyon, secilen aralikla cakisan iki ayri segmentte iki farkli oda tipine (Standart/Suit) atanir.
    private static async Task SeedRezervasyonIkiOdaTipindeAsync(
        StysAppDbContext dbContext,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        string? referansNo = null)
    {
        var ortaTarih = girisTarihi.AddDays(1);

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
                OdaId = 102,
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
