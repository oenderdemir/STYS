using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Raporlar.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Tests;

public class OdaDolulukRaporExcelServiceTests
{
    // Bos bir ay icin Excel binary uretimi bos donmemeli; workbook 4 sheet icermeli.
    [Fact]
    public async Task OlusturAsync_BosAydaDortSheetliWorkbookUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(4, workbook.Worksheets.Count);
        Assert.Contains(workbook.Worksheets, x => x.Name == "Özet");
        Assert.Contains(workbook.Worksheets, x => x.Name == "Oda Planı");
        Assert.Contains(workbook.Worksheets, x => x.Name == "Tahsilatlar");
        Assert.Contains(workbook.Worksheets, x => x.Name == "Rezervasyon Listesi");

        var ozetSheet = workbook.Worksheet("Özet");
        Assert.Equal("Aylık Oda Doluluk ve Tahsilat Raporu", ozetSheet.Cell(1, 1).GetString());

        var listeSheet = workbook.Worksheet("Rezervasyon Listesi");
        Assert.Equal("Tarih", listeSheet.Cell(1, 1).GetString());
        Assert.Equal("Oda No", listeSheet.Cell(1, 2).GetString());
    }

    // Oda Plani sheet'inde matris altinda "TAHSİLATLAR" basligi olmali.
    [Fact]
    public async Task OlusturAsync_OdaPlaniSheetindeTahsilatlarBasligiBulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");
        var tumHucreler = odaPlani.CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains(tumHucreler, x => x == "TAHSİLATLAR");
        Assert.Contains(tumHucreler, x => x == "ODA NUMARASI");
        Assert.Contains(tumHucreler, x => x == "MAKBUZ NO");
        Assert.Contains(tumHucreler, x => x == "ÖDEME YAPAN");
        Assert.Contains(tumHucreler, x => x == "ÜNİTESİ");
        Assert.Contains(tumHucreler, x => x == "TAHSİL EDİLEN");
    }

    // Tahsilat yoksa Oda Plani ve Tahsilatlar sheet'lerinde bilgi mesaji gorunmeli.
    [Fact]
    public async Task OlusturAsync_TahsilatYokIseBilgiMesajiGosterilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");
        var tahsilatlar = workbook.Worksheet("Tahsilatlar");

        Assert.Contains(odaPlani.CellsUsed().Select(c => c.GetString()), x => x == "Bu dönem için tahsilat kaydı bulunamadı.");
        Assert.Contains(tahsilatlar.CellsUsed().Select(c => c.GetString()), x => x == "Bu dönem için tahsilat kaydı bulunamadı.");
    }

    // Tahsilat varsa Oda Plani ve Tahsilatlar sheet'lerinde tutar gorunmeli.
    [Fact]
    public async Task OlusturAsync_TahsilatVarsaTahsilEdilenAlaninaTutarYazilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");
        var tahsilatlar = workbook.Worksheet("Tahsilatlar");

        Assert.Contains(odaPlani.CellsUsed().Select(c => c.GetString()), x => x.Contains("300,00"));
        Assert.Contains(tahsilatlar.CellsUsed().Select(c => c.GetString()), x => x.Contains("300,00"));
        Assert.Equal("Ali Veli", tahsilatlar.Cell(2, 2).GetString());
    }

    // matrisYonu verilmezse (null) Oda Plani sheet'i musteri formatinda (tarih-satir) uretilmeli.
    [Fact]
    public async Task OlusturAsync_MatrisYonuNullIseTarihSatirVarsayilanKullanilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false, matrisYonu: null);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");

        Assert.Equal("TARİH", odaPlani.Cell(1, 1).GetString());
        Assert.Equal("GÜN", odaPlani.Cell(1, 2).GetString());
        Assert.Contains("101", odaPlani.Cell(1, 3).GetString());

        var tumHucreler = odaPlani.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(tumHucreler, x => x.StartsWith("Ali Veli"));
    }

    // matrisYonu = "tarih-satir" acikca verildiginde de ayni musteri formati uretilmeli.
    [Fact]
    public async Task OlusturAsync_MatrisYonuTarihSatirIseTarihSatirFormatiUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false, matrisYonu: "tarih-satir");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");

        Assert.Equal("TARİH", odaPlani.Cell(1, 1).GetString());
        Assert.Equal("GÜN", odaPlani.Cell(1, 2).GetString());
        Assert.Contains("101", odaPlani.Cell(1, 3).GetString());
    }

    // matrisYonu = "oda-satir" ise eski kompakt oda-satir/gun-kolon formati korunmali.
    [Fact]
    public async Task OlusturAsync_MatrisYonuOdaSatirIseEskiKompaktFormatUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false, matrisYonu: "oda-satir");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");

        Assert.Equal("Oda No", odaPlani.Cell(1, 1).GetString());
        Assert.Equal("Oda Tipi", odaPlani.Cell(1, 2).GetString());
        Assert.Equal("101", odaPlani.Cell(2, 1).GetString());
        Assert.Contains("1", odaPlani.Cell(1, 4).GetString());

        var tumHucreler = odaPlani.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(tumHucreler, x => x.StartsWith("Ali Veli"));
    }

    // Tarih-satir (musteri) goruniminde dolu hucrede durum/odeme metni yazilmamali; sadece renkle anlasilmali.
    [Fact]
    public async Task OlusturAsync_TarihSatirGorunumundeDurumMetniYazilmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 100m,
            rezervasyonDurumu: RezervasyonDurumlari.CheckInTamamlandi,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false, matrisYonu: "tarih-satir");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");

        // Sadece veri hucrelerini kontrol et (legend alani ayri sutunlarda, kasten durum metni icerir).
        var doluHucreMetni = odaPlani.Cell(11, 3).GetString();

        Assert.Equal("Ali Veli", doluHucreMetni);
        Assert.DoesNotContain("Onaylı", doluHucreMetni);
        Assert.DoesNotContain("Check-in", doluHucreMetni);
        Assert.DoesNotContain("Eksik", doluHucreMetni);
    }

    // Tarih-satir sheet'inde renk legend'i bulunmali.
    [Fact]
    public async Task OlusturAsync_TarihSatirSheetindeRenkLegendiBulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");
        var tumHucreler = odaPlani.CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains(tumHucreler, x => x == "RENK AÇIKLAMALARI");
    }

    // Rezervasyon Listesi sheet'inde header satiri beklenen kolonlari icermeli.
    [Fact]
    public async Task OlusturAsync_RezervasyonListesiSheetiHeaderIcerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var liste = workbook.Worksheet("Rezervasyon Listesi");

        var headerHucreleri = liste.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Misafir", headerHucreleri);
        Assert.Contains("Ödeme Eksik Mi", headerHucreleri);
        Assert.Contains("Çakışma Var Mı", headerHucreleri);
        Assert.Equal("Ali Veli", liste.Cell(2, 3).GetString());
    }

    // Cakismali hucre icin Excel uretimi hata vermemeli, Oda Plani hucresinde CAKISMA yazmali.
    [Fact]
    public async Task OlusturAsync_CakismaliHucreIcinHataVermezVeCakismaYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 9),
            cikisTarihi: new DateTime(2026, 7, 11),
            toplamUcret: 200m,
            odemeTutari: 200m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ayşe Yılmaz");

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var odaPlani = workbook.Worksheet("Oda Planı");
        var tumHucreler = odaPlani.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(tumHucreler, x => x == "ÇAKIŞMA");
    }

    private static OdaDolulukRaporExcelService CreateExcelService(StysAppDbContext dbContext)
    {
        var raporService = new OdaDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeLicenseService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OdaDolulukRaporExcelService(raporService);
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

        dbContext.Odalar.Add(new Oda
        {
            Id = 100,
            OdaNo = "101",
            BinaId = 10,
            TesisOdaTipiId = 20,
            KatNo = 1,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRezervasyonAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        decimal toplamUcret,
        decimal odemeTutari,
        string rezervasyonDurumu,
        string misafirAdiSoyadi = "Test Misafir")
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"REF-{Guid.NewGuid():N}"[..12],
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            ToplamBazUcret = toplamUcret,
            ToplamUcret = toplamUcret,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = misafirAdiSoyadi,
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
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            KapasiteSnapshot = 2
        });

        if (odemeTutari > 0m)
        {
            dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
            {
                RezervasyonId = rezervasyon.Id,
                OdemeTarihi = girisTarihi,
                OdemeTutari = odemeTutari,
                ParaBirimi = "TRY",
                OdemeTipi = OdemeTipleri.Nakit
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeLicenseService : ILicenseService
    {
        public Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LicenseValidationResult.Failure("test"));

        public Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public void InvalidateCache()
        {
        }

        public Task EnsureLicensedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
