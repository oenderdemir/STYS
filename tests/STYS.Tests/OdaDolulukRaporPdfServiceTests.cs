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

public class OdaDolulukRaporPdfServiceTests
{
    // Bos bir ay icin PDF binary uretimi bos donmemeli.
    [Fact]
    public async Task OlusturAsync_BosAydaPdfBinaryUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext, odaSayisi: 2);

        var service = CreatePdfService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 100);
    }

    // Dolu rezervasyon iceren ay icin PDF binary uretimi bos donmemeli.
    [Fact]
    public async Task OlusturAsync_DoluRezervasyonIcinPdfUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext, odaSayisi: 2);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        var service = CreatePdfService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);
    }

    // Tahsilat iceren ay icin PDF uretimi hata vermemeli.
    [Fact]
    public async Task OlusturAsync_TahsilatIcerenAyIcinHataVermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext, odaSayisi: 2);
        var rezervasyonId = await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 0m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Ali Veli");

        await AddOdemeAsync(dbContext, rezervasyonId, new DateTime(2026, 7, 10), 300m);

        var service = CreatePdfService(dbContext);
        var exception = await Record.ExceptionAsync(() => service.OlusturAsync(1, 2026, 7, maskele: false));

        Assert.Null(exception);
    }

    // Cok oda varsa PDF uretimi hata vermemeli (sayfa basina kolon sinirlamasi devreye girmeli).
    [Fact]
    public async Task OlusturAsync_CokOdaVarsaHataVermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext, odaSayisi: 25);

        var service = CreatePdfService(dbContext);
        var exception = await Record.ExceptionAsync(() => service.OlusturAsync(1, 2026, 7, maskele: false));

        Assert.Null(exception);
    }

    // Turkce karakter iceren misafir adiyla PDF uretimi hata vermemeli.
    [Fact]
    public async Task OlusturAsync_TurkceKarakterliMisafirAdiIleHataVermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedOdaFixtureAsync(dbContext, odaSayisi: 2);
        await SeedRezervasyonAsync(
            dbContext,
            odaId: 100,
            girisTarihi: new DateTime(2026, 7, 10),
            cikisTarihi: new DateTime(2026, 7, 13),
            toplamUcret: 300m,
            odemeTutari: 300m,
            rezervasyonDurumu: RezervasyonDurumlari.Onayli,
            misafirAdiSoyadi: "Şükrü Öztürk Çağlar Ğürbüz İpek");

        var service = CreatePdfService(dbContext);
        var bytes = await service.OlusturAsync(1, 2026, 7, maskele: false);

        Assert.NotEmpty(bytes);
    }

    private static OdaDolulukRaporPdfService CreatePdfService(StysAppDbContext dbContext)
    {
        var raporService = new OdaDolulukRaporService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeLicenseService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger());

        return new OdaDolulukRaporPdfService(raporService);
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase($"stys-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, null, new FakeCurrentTenantAccessor());
    }

    private static async Task SeedOdaFixtureAsync(StysAppDbContext dbContext, int odaSayisi)
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

        for (var i = 0; i < odaSayisi; i++)
        {
            dbContext.Odalar.Add(new Oda
            {
                Id = 100 + i,
                OdaNo = (101 + i).ToString(),
                BinaId = 10,
                TesisOdaTipiId = 20,
                KatNo = 1,
                AktifMi = true
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> SeedRezervasyonAsync(
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

        return rezervasyon.Id;
    }

    private static async Task AddOdemeAsync(StysAppDbContext dbContext, int rezervasyonId, DateTime odemeTarihi, decimal odemeTutari)
    {
        dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
        {
            RezervasyonId = rezervasyonId,
            OdemeTarihi = odemeTarihi,
            OdemeTutari = odemeTutari,
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
