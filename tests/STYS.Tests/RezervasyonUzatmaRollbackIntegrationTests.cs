using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Bildirimler;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.Binalar.Entities;
using STYS.Fiyatlandirma.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.KonaklamaTipleri.Entities;
using STYS.Kurumlar.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.TicariBelgeler.Dtos;
using STYS.OdaSiniflari.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Odalar.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.Rezervasyonlar.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// RezervasyonUzatAsync'in GERCEK SQL Server transactionina karsi atomiklik garantisini dogrular.
/// RezervasyonServiceTests.cs'teki testlerin cogu (bilincli olarak) EF Core InMemory provider
/// kullanir - ancak InMemory provider transactionlari TAMAMEN YOK SAYAR (bkz.
/// InMemoryEventId.TransactionIgnoredWarning): bir SaveChangesAsync cagrisi INMEMORY'de HER ZAMAN
/// KALICI olarak yazilir, transaction commit edilmese/rollback edilse BILE. Bu yuzden "segmentler
/// olusturulduktan SONRA bir hata olursa hepsi birlikte rollback edilir" iddiasi InMemory ile
/// SAHTE olarak dogrulanamaz - bu dosya bu iddiayi GERCEK bir SQL Server'a karsi dogrular.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class RezervasyonUzatmaRollbackIntegrationTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "UZTRB-118";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _tesisId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = CreateDbContext();

        var kurum = new Kurum { Kod = _uniqueSuffix, Ad = "Test Kurum " + _uniqueSuffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + _uniqueSuffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();
        _kurumId = kurum.Id;

        var tesis = new Tesis
        {
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis " + _uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();
        _tesisId = tesis.Id;

        var misafirTipi = new MisafirTipi { Kod = _uniqueSuffix + "-MT", Ad = "Test Misafir " + _uniqueSuffix, AktifMi = true };
        var konaklamaTipi = new KonaklamaTipi { Kod = _uniqueSuffix + "-KT", Ad = "Test Konaklama " + _uniqueSuffix, AktifMi = true };
        dbContext.MisafirTipleri.Add(misafirTipi);
        dbContext.KonaklamaTipleri.Add(konaklamaTipi);
        await dbContext.SaveChangesAsync();

        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi { TesisId = tesis.Id, MisafirTipiId = misafirTipi.Id, AktifMi = true });
        dbContext.TesisKonaklamaTipleri.Add(new TesisKonaklamaTipi { TesisId = tesis.Id, KonaklamaTipiId = konaklamaTipi.Id, AktifMi = true });

        var odaSinifi = new OdaSinifi { Kod = _uniqueSuffix + "-OS", Ad = "Test Sinif " + _uniqueSuffix, AktifMi = true };
        dbContext.OdaSiniflari.Add(odaSinifi);
        await dbContext.SaveChangesAsync();

        var bina = new Bina { TesisId = tesis.Id, Ad = "Test Blok " + _uniqueSuffix, KatSayisi = 1, AktifMi = true };
        dbContext.Binalar.Add(bina);
        await dbContext.SaveChangesAsync();

        var odaTipi = new OdaTipi { TesisId = tesis.Id, OdaSinifiId = odaSinifi.Id, Ad = "Test Oda Tipi " + _uniqueSuffix, Kapasite = 1, PaylasimliMi = false, AktifMi = true };
        dbContext.OdaTipleri.Add(odaTipi);
        await dbContext.SaveChangesAsync();

        var odaEski = new Oda { OdaNo = "ESKI-" + _uniqueSuffix, BinaId = bina.Id, TesisOdaTipiId = odaTipi.Id, KatNo = 1, AktifMi = true };
        var odaYeni = new Oda { OdaNo = "YENI-" + _uniqueSuffix, BinaId = bina.Id, TesisOdaTipiId = odaTipi.Id, KatNo = 1, AktifMi = true };
        dbContext.Odalar.AddRange(odaEski, odaYeni);
        await dbContext.SaveChangesAsync();

        dbContext.OdaFiyatlari.Add(new OdaFiyat
        {
            TesisOdaTipiId = odaTipi.Id,
            KonaklamaTipiId = konaklamaTipi.Id,
            MisafirTipiId = misafirTipi.Id,
            KisiSayisi = 1,
            Fiyat = 500m,
            ParaBirimi = "TRY",
            BaslangicTarihi = new DateTime(2020, 1, 1),
            BitisTarihi = new DateTime(2030, 12, 31),
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        OdaEskiId = odaEski.Id;
        OdaYeniId = odaYeni.Id;
        MisafirTipiId = misafirTipi.Id;
        KonaklamaTipiId = konaklamaTipi.Id;
    }

    private int OdaEskiId;
    private int OdaYeniId;
    private int MisafirTipiId;
    private int KonaklamaTipiId;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = CreateDbContext();

        await dbContext.RezervasyonKonaklayanSegmentAtamalari
            .Where(x => x.RezervasyonKonaklayan != null && x.RezervasyonKonaklayan.Rezervasyon != null && x.RezervasyonKonaklayan.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonKonaklayanlar
            .Where(x => x.Rezervasyon != null && x.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonSegmentOdaAtamalari
            .Where(x => x.RezervasyonSegment != null && x.RezervasyonSegment.Rezervasyon != null && x.RezervasyonSegment.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonSegmentleri
            .Where(x => x.Rezervasyon != null && x.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonKonaklamaHaklari
            .Where(x => x.Rezervasyon != null && x.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonDegisiklikGecmisleri
            .Where(x => x.Rezervasyon != null && x.Rezervasyon.TesisId == _tesisId)
            .ExecuteDeleteAsync();
        await dbContext.Rezervasyonlar.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.OdaFiyatlari.Where(x => x.MisafirTipiId == MisafirTipiId).ExecuteDeleteAsync();
        await dbContext.Odalar.Where(x => x.Id == OdaEskiId || x.Id == OdaYeniId).ExecuteDeleteAsync();
        await dbContext.OdaTipleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.Binalar.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.OdaSiniflari.Where(x => x.Kod == _uniqueSuffix + "-OS").ExecuteDeleteAsync();
        await dbContext.TesisMisafirTipleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.TesisKonaklamaTipleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.MisafirTipleri.Where(x => x.Id == MisafirTipiId).ExecuteDeleteAsync();
        await dbContext.KonaklamaTipleri.Where(x => x.Id == KonaklamaTipiId).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == _tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Ad != null && x.Ad.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == _kurumId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Oda degisimli (CheckoutGunundeOdaDegisimi) bir uzatma sec; en az bir yeni segment ve oda/
    /// konaklayan atamasi ara SaveChanges cagrilariyla veritabanina yazildiktan SONRA (degisiklik
    /// gecmisi kaydi eklenmeye calisilirken), interceptor araciligiyla KONTROLLU bir hata olusturulur.
    /// RezervasyonUzatAsync hata firlatmalidir VE rezervasyon cikis tarihi, toplam ucretler,
    /// segmentler, oda/konaklayan/yatak atamalari, konaklama haklari ve Uzatildi gecmis kaydinin
    /// TAMAMI, YENI BIR DbContext ile veritabanindan tekrar okundugunda ESKI HALDE kalmis olmalidir -
    /// bu, GERCEK iliskisel transactionin (Serializable + sp_getapplock) atomikligini dogrular.
    /// </summary>
    [IntegrationFact]
    public async Task RezervasyonUzatAsync_SegmentlerOlusturulduktanSonraSaveChangesHataVerirse_TumTransactionRollbackEdilir()
    {
        var girisTarihi = new DateTime(2026, 5, 8, 14, 0, 0);
        var eskiCikisTarihi = new DateTime(2026, 5, 9, 10, 0, 0);
        var yeniCikisTarihi = new DateTime(2026, 5, 10, 10, 0, 0);

        int rezervasyonId;
        await using (var seedContext = CreateDbContext())
        {
            var rezervasyon = new Rezervasyon
            {
                ReferansNo = $"{TestMarker}-{Guid.NewGuid():N}"[..20],
                TesisId = _tesisId,
                KisiSayisi = 1,
                MisafirTipiId = MisafirTipiId,
                KonaklamaTipiId = KonaklamaTipiId,
                GirisTarihi = girisTarihi,
                CikisTarihi = eskiCikisTarihi,
                MisafirAdiSoyadi = "Rollback Test",
                MisafirTelefon = "0000",
                ToplamBazUcret = 500m,
                ToplamUcret = 500m,
                ParaBirimi = "TRY",
                RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi,
                AktifMi = true
            };
            seedContext.Rezervasyonlar.Add(rezervasyon);
            await seedContext.SaveChangesAsync();
            rezervasyonId = rezervasyon.Id;

            var segment = new RezervasyonSegment { RezervasyonId = rezervasyonId, SegmentSirasi = 1, BaslangicTarihi = girisTarihi, BitisTarihi = eskiCikisTarihi };
            seedContext.RezervasyonSegmentleri.Add(segment);
            await seedContext.SaveChangesAsync();

            seedContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
            {
                RezervasyonSegmentId = segment.Id,
                OdaId = OdaEskiId,
                AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "ESKI-" + _uniqueSuffix,
                BinaAdiSnapshot = "Test Blok " + _uniqueSuffix,
                OdaTipiAdiSnapshot = "Test Oda Tipi " + _uniqueSuffix,
                PaylasimliMiSnapshot = false,
                KapasiteSnapshot = 1
            });
            await seedContext.SaveChangesAsync();

            var konaklayan = new RezervasyonKonaklayan { RezervasyonId = rezervasyonId, SiraNo = 1, AdSoyad = "Rollback Misafir", KatilimDurumu = KonaklayanKatilimDurumlari.Geldi };
            seedContext.RezervasyonKonaklayanlar.Add(konaklayan);
            await seedContext.SaveChangesAsync();

            seedContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
            {
                RezervasyonKonaklayanId = konaklayan.Id,
                RezervasyonSegmentId = segment.Id,
                OdaId = OdaEskiId
            });
            await seedContext.SaveChangesAsync();

            // Uzatma boyunca ESKI oda baska bir rezervasyona bagli - oda degisimi ZORUNLU olur,
            // bu da en az bir YENI segment + yeni oda/konaklayan atamasi olusturulmasini garantiler.
            var digerRezervasyon = new Rezervasyon
            {
                ReferansNo = $"{TestMarker}-DGR-{Guid.NewGuid():N}"[..20],
                TesisId = _tesisId,
                KisiSayisi = 1,
                MisafirTipiId = MisafirTipiId,
                KonaklamaTipiId = KonaklamaTipiId,
                GirisTarihi = eskiCikisTarihi,
                CikisTarihi = yeniCikisTarihi,
                MisafirAdiSoyadi = "Diger Misafir",
                MisafirTelefon = "0000",
                ToplamBazUcret = 500m,
                ToplamUcret = 500m,
                ParaBirimi = "TRY",
                RezervasyonDurumu = RezervasyonDurumlari.Onayli,
                AktifMi = true
            };
            seedContext.Rezervasyonlar.Add(digerRezervasyon);
            await seedContext.SaveChangesAsync();

            var digerSegment = new RezervasyonSegment { RezervasyonId = digerRezervasyon.Id, SegmentSirasi = 1, BaslangicTarihi = eskiCikisTarihi, BitisTarihi = yeniCikisTarihi };
            seedContext.RezervasyonSegmentleri.Add(digerSegment);
            await seedContext.SaveChangesAsync();

            seedContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
            {
                RezervasyonSegmentId = digerSegment.Id,
                OdaId = OdaEskiId,
                AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "ESKI-" + _uniqueSuffix,
                BinaAdiSnapshot = "Test Blok " + _uniqueSuffix,
                OdaTipiAdiSnapshot = "Test Oda Tipi " + _uniqueSuffix,
                PaylasimliMiSnapshot = false,
                KapasiteSnapshot = 1
            });
            await seedContext.SaveChangesAsync();
        }

        // Kayittan ONCEKI durumu, AYRI bir context ile oku (karsilastirma referansi).
        RezervasyonSnapshot oncekiDurum;
        await using (var readContext = CreateDbContext())
        {
            oncekiDurum = await ReadSnapshotAsync(readContext, rezervasyonId);
        }

        // GERCEK RezervasyonUzatAsync akisini, segmentler/atamalar olusturulduktan SONRA (degisiklik
        // gecmisi eklenirken) SaveChanges'in hata vermesini SIMULE eden bir interceptor ile calistir.
        await using (var uzatmaContext = CreateDbContext(new ThrowOnHistoryInsertInterceptor()))
        {
            var service = CreateRezervasyonService(uzatmaContext);

            var secenekler = await service.GetUzatmaSecenekleriAsync(
                rezervasyonId,
                new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikisTarihi });
            var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                service.RezervasyonUzatAsync(rezervasyonId, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikisTarihi, SenaryoKodu = secim.SenaryoKodu }));
        }

        // Context'i TEMIZLE ve YENI bir DbContext ile veritabanini TEKRAR OKU - hicbir sey degismemis
        // olmali (gercek SQL Server transactioni tum ara SaveChanges cagrilarini geri almis olmali).
        await using var dogrulamaContext = CreateDbContext();
        var sonrakiDurum = await ReadSnapshotAsync(dogrulamaContext, rezervasyonId);

        Assert.Equal(oncekiDurum.CikisTarihi, sonrakiDurum.CikisTarihi);
        Assert.Equal(oncekiDurum.ToplamBazUcret, sonrakiDurum.ToplamBazUcret);
        Assert.Equal(oncekiDurum.ToplamUcret, sonrakiDurum.ToplamUcret);
        Assert.Equal(oncekiDurum.SegmentSayisi, sonrakiDurum.SegmentSayisi);
        Assert.Equal(oncekiDurum.SegmentOdaAtamaSayisi, sonrakiDurum.SegmentOdaAtamaSayisi);
        Assert.Equal(oncekiDurum.KonaklayanSegmentAtamaSayisi, sonrakiDurum.KonaklayanSegmentAtamaSayisi);
        Assert.Equal(oncekiDurum.KonaklamaHakkiSayisi, sonrakiDurum.KonaklamaHakkiSayisi);
        Assert.Equal(oncekiDurum.UzatildiGecmisKaydiSayisi, sonrakiDurum.UzatildiGecmisKaydiSayisi);
        Assert.Equal(0, sonrakiDurum.UzatildiGecmisKaydiSayisi);
    }

    private sealed record RezervasyonSnapshot(
        DateTime CikisTarihi,
        decimal ToplamBazUcret,
        decimal ToplamUcret,
        int SegmentSayisi,
        int SegmentOdaAtamaSayisi,
        int KonaklayanSegmentAtamaSayisi,
        int KonaklamaHakkiSayisi,
        int UzatildiGecmisKaydiSayisi);

    private static async Task<RezervasyonSnapshot> ReadSnapshotAsync(StysAppDbContext dbContext, int rezervasyonId)
    {
        var rezervasyon = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == rezervasyonId);
        var segmentSayisi = await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == rezervasyonId);
        var segmentOdaAtamaSayisi = await dbContext.RezervasyonSegmentOdaAtamalari
            .CountAsync(x => x.RezervasyonSegment != null && x.RezervasyonSegment.RezervasyonId == rezervasyonId);
        var konaklayanSegmentAtamaSayisi = await dbContext.RezervasyonKonaklayanSegmentAtamalari
            .CountAsync(x => x.RezervasyonKonaklayan != null && x.RezervasyonKonaklayan.RezervasyonId == rezervasyonId);
        var konaklamaHakkiSayisi = await dbContext.RezervasyonKonaklamaHaklari.CountAsync(x => x.RezervasyonId == rezervasyonId);
        var uzatildiSayisi = await dbContext.RezervasyonDegisiklikGecmisleri
            .CountAsync(x => x.RezervasyonId == rezervasyonId && x.IslemTipi == RezervasyonGecmisIslemTipleri.Uzatildi);

        return new RezervasyonSnapshot(
            rezervasyon.CikisTarihi,
            rezervasyon.ToplamBazUcret,
            rezervasyon.ToplamUcret,
            segmentSayisi,
            segmentOdaAtamaSayisi,
            konaklayanSegmentAtamaSayisi,
            konaklamaHakkiSayisi,
            uzatildiSayisi);
    }

    /// <summary>
    /// RezervasyonDegisiklikGecmisi eklenmeye calisildigi (Uzatildi gecmis kaydi - RezervasyonUzatAsync
    /// akisinin SON SaveChangesAsync cagrisi, segmentler/atamalar cogunlukla ONCEKI SaveChanges
    /// cagrilariyla ZATEN yazilmis oldugu icin) anda KASITLI olarak hata firlatir - boylece GERCEK
    /// bir SaveChanges basarisizligi (ör. gecici bir DB hatasi) SIMULE edilir.
    /// </summary>
    private sealed class ThrowOnHistoryInsertInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ThrowIfHistoryBeingInserted(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            ThrowIfHistoryBeingInserted(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ThrowIfHistoryBeingInserted(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            var hasHistoryInsert = context.ChangeTracker.Entries<RezervasyonDegisiklikGecmisi>()
                .Any(x => x.State == EntityState.Added);

            if (hasHistoryInsert)
            {
                throw new InvalidOperationException("Test: SaveChangesAsync sirasinda simule edilen hata (degisiklik gecmisi eklenirken).");
            }
        }
    }

    private static StysAppDbContext CreateDbContext(SaveChangesInterceptor? interceptor = null)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{IntegrationFactAttribute.ConnectionStringEnvVar} ortam degiskeni tanimli degil.");
        }

        var builder = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new StysAppDbContext(builder.Options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static RezervasyonService CreateRezervasyonService(StysAppDbContext dbContext)
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        return new RezervasyonService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeBildirimService(),
            httpContextAccessor,
            new FakeLicenseService(),
            new FakeCurrentTenantAccessor(),
            new NoOpDomainOperationLogger(),
            new FakeRezervasyonOdemeMuhasebeService(),
            new FakeRezervasyonGelirTahakkukService());
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "integration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeBildirimService : IBildirimService
    {
        public Task<List<BildirimDto>> GetCurrentUserBildirimlerAsync(int take = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<BildirimDto>());

        public Task<int> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<BildirimTercihDto> GetCurrentUserTercihAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BildirimTercihDto());

        public Task<BildirimTercihDto> UpdateCurrentUserTercihAsync(BildirimTercihGuncelleRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BildirimTercihDto());

        public Task MarkAsReadAsync(int bildirimId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishToTesisUsersAsync(int tesisId, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishToUsersAsync(IEnumerable<Guid> userIds, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

    private sealed class NoOpDomainOperationLogger : IDomainOperationLogger
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

    private sealed class FakeRezervasyonOdemeMuhasebeService : IRezervasyonOdemeMuhasebeService
    {
        public Task TahsilatOlusturAsync(
            Rezervasyon rezervasyon,
            RezervasyonOdeme odeme,
            int? kasaBankaHesapId,
            int? cariKartIdOverride,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task TahsilatIptalEtAsync(RezervasyonOdeme odeme, string? iptalAciklama, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeRezervasyonGelirTahakkukService : IRezervasyonGelirTahakkukService
    {
        public Task<TicariBelgeDetayDto> OlusturTaslakAsync(int rezervasyonId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TicariBelgeDetayDto { Id = 1, BelgeNo = "TEST-1" });

        public Task<RezervasyonGelirOzetiDto> GetGelirOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RezervasyonGelirOzetiDto { RezervasyonId = rezervasyonId });

        public Task<RezervasyonTahsilatKapamaSonucuDto> KapatOncekiTahsilatlariAsync(int rezervasyonId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RezervasyonTahsilatKapamaSonucuDto());
    }
}
