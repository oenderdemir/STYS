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
    // Bos bir ay icin Excel binary uretimi bos donmemeli, sheet olusmali.
    [Fact]
    public async Task OlusturAsync_BosAydaExcelBinaryUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext);

        var service = CreateExcelService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = Assert.Single(workbook.Worksheets);
        Assert.Equal("Aylık Oda Planı", ws.Name);
        Assert.Equal("Aylık Oda Doluluk ve Tahsilat Raporu", ws.Cell(1, 1).GetString());
    }

    // Dolu rezervasyon iceren ay icin Excel ozet alaninda dogru degerler yer almali.
    [Fact]
    public async Task OlusturAsync_DoluRezervasyonIcinOzetDegerleriDoner()
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

        Assert.NotEmpty(bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheets.Single();

        var tumHucreler = ws.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(tumHucreler, x => x.Contains("Dolu Oda/Gün"));
        Assert.Contains(tumHucreler, x => x.Contains("Ali Veli"));
    }

    // Cakismali hucre icin Excel uretimi hata vermemeli.
    [Fact]
    public async Task OlusturAsync_CakismaliHucreIcinHataVermez()
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
        var ws = workbook.Worksheets.Single();
        var tumHucreler = ws.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(tumHucreler, x => x.Contains("ÇAKIŞMA VAR"));
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
