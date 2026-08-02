using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeFaz1IntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-1";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriHesapId;
    private int _tedarikciHesapId;
    private int _musteriKartId;
    private int _tedarikciKartId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, tedarikciHesap, gelirHesap, kdvHesap);
        await dbContext.SaveChangesAsync();
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        musteriKart.EArsivKapsamindaMi = true;
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        tedarikciKart.EFaturaMukellefiMi = true;
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _tedarikciKartId = tedarikciKart.Id;

        dbContext.MuhasebeDonemler.AddRange(
            new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
            {
                TesisId = _tesisId,
                MaliYil = 2026,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1),
                BitisTarihi = new DateTime(2026, 12, 31),
                KapaliMi = false
            });
        dbContext.KurumFaturaNumaraSayaclari.Add(new STYS.Muhasebe.SatisBelgeleri.Entities.KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId,
            MaliYil = 2026,
            SeriKodu = "EBF",
            SonNumara = 0,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<CariKartProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger());
    }

    private static StysAppDbContext CreateDbContext(
        IInterceptor? interceptor = null,
        int? currentKurumId = null,
        bool isSuperAdmin = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor(currentKurumId, isSuperAdmin));
    }

    private CreateSatisBelgesiRequest BuildSatisBelgesiRequest(SatisBelgesiTipi belgeTipi = SatisBelgesiTipi.SatisFaturasi)
        => new()
        {
            BelgeNo = $"EBF-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = belgeTipi,
            TesisId = _tesisId,
            CariKartId = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? _tedarikciKartId : _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satiri",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

    private async Task<SatisBelgesiDto> CreateAndApproveOutgoingInvoiceAsync()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);
        return await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
    }

    [Fact]
    public async Task AyniBelgeIkiKezKesildiginde_TekNumaraTekUuidTekKayitOlusur()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var ilk = await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
        var ikinci = await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);

        Assert.Equal(ilk.ResmiFaturaNo, ikinci.ResmiFaturaNo);
        Assert.Equal(ilk.EBelgeUuid, ikinci.EBelgeUuid);

        var belgeDb = await dbContext.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .ThenInclude(x => x!.Snapshot)
            .SingleAsync(x => x.Id == created.Id.Value);

        Assert.NotNull(belgeDb.EBelgeKaydi);
        Assert.NotNull(belgeDb.EBelgeKaydi!.Snapshot);
        Assert.Equal(1, await dbContext.EBelgeKayitlari.CountAsync(x => x.SatisBelgesiId == created.Id.Value));
        Assert.Equal(1, await dbContext.EBelgeSnapshots.CountAsync(x => x.EBelgeKaydiId == belgeDb.EBelgeKaydi!.Id));
        Assert.Equal(ilk.ResmiFaturaNo, belgeDb.ResmiFaturaNo);
        Assert.Equal(ilk.EBelgeUuid, belgeDb.EBelgeKaydi.EBelgeUuid);
    }

    private sealed class SatisBelgesiSelectBarrierInterceptor : DbCommandInterceptor
    {
        private readonly SemaphoreSlim _gate;
        private readonly CountdownEvent _ready;
        private bool _triggered;

        public SatisBelgesiSelectBarrierInterceptor(SemaphoreSlim gate, CountdownEvent ready)
        {
            _gate = gate;
            _ready = ready;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_triggered
                && command.CommandText.Contains("SatisBelgeleri", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                _triggered = true;
                _ready.Signal();
                await _gate.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task IkiDbContextEszamanliKesimdeMukerrerKayitOlusmaz()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var seedCtx = CreateDbContext();
        var seedService = CreateService(seedCtx);
        var created = await seedService.CreateAsync(BuildSatisBelgesiRequest());
        await seedService.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await seedService.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var gate = new SemaphoreSlim(0, 2);
        var ready = new CountdownEvent(2);
        var interceptor1 = new SatisBelgesiSelectBarrierInterceptor(gate, ready);
        var interceptor2 = new SatisBelgesiSelectBarrierInterceptor(gate, ready);

        await using var ctx1 = CreateDbContext(interceptor1);
        await using var ctx2 = CreateDbContext(interceptor2);
        var service1 = CreateService(ctx1);
        var service2 = CreateService(ctx2);

        var task1 = service1.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
        var task2 = service2.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);

        Assert.True(ready.Wait(TimeSpan.FromSeconds(30)));
        gate.Release(2);

        var sonuc1 = await task1;
        var sonuc2 = await task2;

        Assert.Equal(sonuc1.ResmiFaturaNo, sonuc2.ResmiFaturaNo);
        Assert.Equal(sonuc1.EBelgeUuid, sonuc2.EBelgeUuid);

        await using var verifyCtx = CreateDbContext();
        Assert.Equal(1, await verifyCtx.EBelgeKayitlari.CountAsync(x => x.SatisBelgesiId == created.Id.Value));
        Assert.Equal(1, await verifyCtx.EBelgeSnapshots.CountAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value));
    }

    [Fact]
    public async Task KesimSonrasiMasterDataDegisseDeCanonicalJsonVeHashDegismez()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        var cut = await CreateAndApproveOutgoingInvoiceAsync();

        await using var snapshotCtx = CreateDbContext();
        var snapshot = await snapshotCtx.EBelgeSnapshots
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

        var canonicalJsonOnce = snapshot.CanonicalJson;
        var shaOnce = snapshot.CanonicalSha256;

        await using var mutateCtx = CreateDbContext();
        var kurum = await mutateCtx.Kurumlar.SingleAsync(x => x.Id == _kurumId);
        var tesis = await mutateCtx.Tesisler.SingleAsync(x => x.Id == _tesisId);
        var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
        kurum.Ad = kurum.Ad + " - degisti";
        tesis.Adres = tesis.Adres + " - degisti";
        cariKart.UnvanAdSoyad = cariKart.UnvanAdSoyad + " - degisti";
        await mutateCtx.SaveChangesAsync();

        await using var verifyCtx = CreateDbContext();
        var snapshotAfter = await verifyCtx.EBelgeSnapshots
            .AsNoTracking()
            .SingleAsync(x => x.EBelgeKaydiId == snapshot.EBelgeKaydiId);

        Assert.Equal(canonicalJsonOnce, snapshotAfter.CanonicalJson);
        Assert.Equal(shaOnce, snapshotAfter.CanonicalSha256);
    }

    [Fact]
    public async Task EBelgeSnapshotUpdateVeyaDeleteEdilemez()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        var cut = await CreateAndApproveOutgoingInvoiceAsync();

        await using var dbContext = CreateDbContext();
        var snapshot = await dbContext.EBelgeSnapshots.SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);
        snapshot.CanonicalJson = snapshot.CanonicalJson + "x";
        await Assert.ThrowsAsync<BaseException>(() => dbContext.SaveChangesAsync());

        await using var deleteCtx = CreateDbContext();
        var snapshotToDelete = await deleteCtx.EBelgeSnapshots.SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);
        deleteCtx.EBelgeSnapshots.Remove(snapshotToDelete);
        await Assert.ThrowsAsync<BaseException>(() => deleteCtx.SaveChangesAsync());
    }

    [Fact]
    public async Task GelenBelgeIcinEBelgeKaydiOlusmaz()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest(SatisBelgesiTipi.AlisFaturasi));

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Contains("giden belgeler", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await dbContext.EBelgeKayitlari.AnyAsync(x => x.SatisBelgesiId == created.Id.Value));
    }

    [Fact]
    public async Task TenantDisiSatisBelgesineEBelgeKaydiBaglanamaz()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var seedCtx = CreateDbContext();
        var seedService = CreateService(seedCtx);
        var created = await seedService.CreateAsync(BuildSatisBelgesiRequest());
        await seedService.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await seedService.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        await using var isolatedCtx = CreateDbContext(currentKurumId: _kurumId + 999, isSuperAdmin: false);
        var isolatedService = CreateService(isolatedCtx);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => isolatedService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Contains("Satış belgesi bulunamadı", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftDeleteEdilmisKayitNumaraVeUuidIcinYenidenKullanimYapilamaz()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        var cut = await CreateAndApproveOutgoingInvoiceAsync();
        var resmiFaturaNo = cut.ResmiFaturaNo!;
        var eBelgeUuid = cut.EBelgeUuid!;

        await using (var softDeleteCtx = CreateDbContext())
        {
            var belge = await softDeleteCtx.SatisBelgeleri
                .IgnoreQueryFilters()
                .Include(x => x.EBelgeKaydi)
                .SingleAsync(x => x.Id == cut.Id.Value);

            softDeleteCtx.Remove(belge.EBelgeKaydi!);
            softDeleteCtx.Remove(belge);
            Assert.True(belge.IsDeleted == false);
            await softDeleteCtx.SaveChangesAsync();
        }

        await using (var insertCtx = CreateDbContext())
        {
            var belge = new SatisBelgesi
            {
                KurumId = _kurumId,
                BelgeNo = $"YENI-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                Durum = SatisBelgesiDurumu.FaturaKesildi,
                TicariDurum = TicariBelgeDurumu.Hazir,
                MuhasebeDurumu = TicariBelgeMuhasebeDurumu.Onaylandi,
                FaturalamaDurumu = TicariBelgeFaturalamaDurumu.Kesildi,
                TesisId = _tesisId,
                CariKartId = _musteriKartId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                FaturaKesimTarihi = DateTime.UtcNow,
                ResmiFaturaNo = resmiFaturaNo
            };
            insertCtx.SatisBelgeleri.Add(belge);
            await Assert.ThrowsAsync<DbUpdateException>(() => insertCtx.SaveChangesAsync());
        }

        await using (var uuidCtx = CreateDbContext())
        {
            var belge = await uuidCtx.SatisBelgeleri
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == cut.Id.Value);
            Assert.True(belge.IsDeleted);
            var ikinciBelge = new SatisBelgesi
            {
                KurumId = _kurumId,
                BelgeNo = $"YENI2-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                Durum = SatisBelgesiDurumu.FaturaKesildi,
                TicariDurum = TicariBelgeDurumu.Hazir,
                MuhasebeDurumu = TicariBelgeMuhasebeDurumu.Onaylandi,
                FaturalamaDurumu = TicariBelgeFaturalamaDurumu.Kesildi,
                TesisId = _tesisId,
                CariKartId = _musteriKartId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                FaturaKesimTarihi = DateTime.UtcNow
            };
            uuidCtx.SatisBelgeleri.Add(ikinciBelge);
            await uuidCtx.SaveChangesAsync();
            uuidCtx.EBelgeKayitlari.Add(new EBelgeKaydi
            {
                KurumId = _kurumId,
                SatisBelgesiId = ikinciBelge.Id,
                EBelgeUuid = eBelgeUuid,
                EBelgeKanali = EBelgeKanali.EArsiv,
                Durum = EBelgeKaydiDurumu.SnapshotHazir
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => uuidCtx.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task AyniKurumdaNullResmiFaturaNoIleIkiBelgeOluşturulabilir()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var ilk = await service.CreateAsync(BuildSatisBelgesiRequest());
        var ikinci = await service.CreateAsync(BuildSatisBelgesiRequest());

        Assert.Null(ilk.ResmiFaturaNo);
        Assert.Null(ikinci.ResmiFaturaNo);
        Assert.Equal(
            2,
            await dbContext.SatisBelgeleri.CountAsync(x => x.Id == ilk.Id!.Value || x.Id == ikinci.Id!.Value));
    }

    [Fact]
    public async Task KanalBayraklariIkisiDeFalseIseKesimTamamenRollbackOlur()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var seedCtx = CreateDbContext();
        var service = CreateService(seedCtx);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var sayacOnce = await seedCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");

        await using (var mutateCtx = CreateDbContext())
        {
            var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
            cariKart.EFaturaMukellefiMi = false;
            cariKart.EArsivKapsamindaMi = false;
            await mutateCtx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Contains("e-Fatura ya da e-Arşiv", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.Id == created.Id.Value);

        Assert.Null(belge.ResmiFaturaNo);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, belge.FaturalamaDurumu);
        Assert.Null(belge.EBelgeKaydi);
        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");
        Assert.Equal(sayacOnce.SonNumara, sayacSonra.SonNumara);
    }

    [Fact]
    public async Task SnapshotAliciAlanlariSatisBelgesiSnapshotindanGelsin()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var seedCtx = CreateDbContext();
        var service = CreateService(seedCtx);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest());

        var orijinalUnvan = created.MusteriUnvan;
        var orijinalAdres = created.MusteriAdres;
        var orijinalTelefon = created.MusteriTelefon;
        var orijinalEposta = created.MusteriEposta;

        await using (var mutateCtx = CreateDbContext())
        {
            var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
            cariKart.UnvanAdSoyad = "CANLI-CARI-KART-DEGISTI";
            cariKart.Adres = "CANLI-ADRES-DEGISTI";
            cariKart.Telefon = "05000000000";
            cariKart.Eposta = "canli-degisti@example.com";
            await mutateCtx.SaveChangesAsync();
        }

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);
        await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);

        await using var snapshotCtx = CreateDbContext();
        var snapshotJson = await snapshotCtx.EBelgeSnapshots
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value);

        using var document = JsonDocument.Parse(snapshotJson.CanonicalJson);
        var alici = document.RootElement.GetProperty("alici");

        Assert.Equal(orijinalUnvan, GetNullableString(alici, "musteriUnvan"));
        Assert.Equal(orijinalAdres, GetNullableString(alici, "musteriAdres"));
        Assert.Equal(orijinalTelefon, GetNullableString(alici, "musteriTelefon"));
        Assert.Equal(orijinalEposta, GetNullableString(alici, "musteriEposta"));
        Assert.NotEqual("CANLI-CARI-KART-DEGISTI", GetNullableString(alici, "musteriUnvan"));
    }

    [Fact]
    public async Task CrossoverTenantEBelgeKaydiVeSnapshotDbTarafindanReddedilir()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var seedCtx = CreateDbContext();
        var service = CreateService(seedCtx);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest());

        await using (var invalidKayitCtx = CreateDbContext())
        {
            invalidKayitCtx.EBelgeKayitlari.Add(new EBelgeKaydi
            {
                KurumId = _kurumId + 999,
                SatisBelgesiId = created.Id!.Value,
                EBelgeUuid = Guid.NewGuid().ToString("D"),
                EBelgeKanali = EBelgeKanali.EArsiv,
                Durum = EBelgeKaydiDurumu.SnapshotHazir
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => invalidKayitCtx.SaveChangesAsync());
        }

        await using (var snapshotCtx = CreateDbContext())
        {
            var kayit = new EBelgeKaydi
            {
                KurumId = _kurumId,
                SatisBelgesiId = created.Id!.Value,
                EBelgeUuid = Guid.NewGuid().ToString("D"),
                EBelgeKanali = EBelgeKanali.EArsiv,
                Durum = EBelgeKaydiDurumu.SnapshotHazir
            };

            snapshotCtx.EBelgeKayitlari.Add(kayit);
            await snapshotCtx.SaveChangesAsync();

            snapshotCtx.EBelgeSnapshots.Add(new EBelgeSnapshot
            {
                KurumId = _kurumId + 999,
                EBelgeKaydiId = kayit.Id,
                BelgeVersiyonu = 2,
                SnapshotSchemaVersion = "1",
                CanonicalJson = "{}",
                CanonicalSha256 = new string('a', 64)
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => snapshotCtx.SaveChangesAsync());
        }
    }

    private static string? GetNullableString(JsonElement parent, string propertyName)
    {
        var property = parent.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }

    private sealed class TestTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int? _currentKurumId;
        private readonly bool _isSuperAdmin;

        public TestTenantAccessor(int? currentKurumId, bool isSuperAdmin)
        {
            _currentKurumId = currentKurumId;
            _isSuperAdmin = isSuperAdmin;
        }

        public int? GetCurrentKurumId() => _currentKurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => _currentKurumId.HasValue ? [_currentKurumId.Value] : [];
        public bool IsSuperAdmin() => _isSuperAdmin;
        public bool IsKurumAdmin() => false;
    }
}
