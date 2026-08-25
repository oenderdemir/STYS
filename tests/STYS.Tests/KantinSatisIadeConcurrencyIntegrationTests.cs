using System.Data.Common;
using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.KantinYonetimi.Kantinler.Mapping;
using STYS.KantinYonetimi.KantinSatislari.Entities;
using STYS.KantinYonetimi.KantinSatislari.Mapping;
using STYS.KantinYonetimi.KantinSatislari.Repositories;
using STYS.KantinYonetimi.KantinSatislari.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokLotlari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// K3C2A final follow-up: aynı iadenin İKİ eşzamanlı kesinleştirme çağrısı, finalize transaction'ı
/// içinde UPDLOCK + ROWLOCK + HOLDLOCK ile alınan kilit sayesinde yalnızca TEK stok hareketi üretir
/// ve ikinci çağrı idempotent olarak aynı sonucu görür. Gerçek SQL Server'a karşı, iki ayrı
/// DbContext ile kanıtlanır (InMemory kilitlemeyi desteklemez).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class KantinSatisIadeConcurrencyIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "KNTIADEYARIS-944";

    private string _uniqueSuffix = TestMarker;
    private int _tesisId = 0;
    private int _depoId = 0;
    private int _kantinId = 0;
    private int _kantinSatisId = 0;
    private int _satisSatirId = 0;
    private int _tasinirKartId = 0;
    private int _iadeId = 0;

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return Task.CompletedTask;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _tesisId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        await dbContext.KantinSatisIadeSatirlari.Where(x => x.StokKodu.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.KantinSatisIadeleri.IgnoreQueryFilters().Where(x => x.Aciklama != null && x.Aciklama.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.KantinSatisSatirlari.Where(x => x.StokKodu.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.KantinSatislar.IgnoreQueryFilters().Where(x => x.Aciklama != null && x.Aciklama.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.StokHareketleri.IgnoreQueryFilters().Where(x => x.Aciklama != null && x.Aciklama.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.KantinUrunler.Where(x => x.Barkod != null && x.Barkod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.KantinSatisNoktalari.Where(x => x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Kantinler.IgnoreQueryFilters().Where(x => x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.StokKodu.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.TamKod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId);
    }

    private async Task SeedAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (_, _, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _tesisId = tesis.Id;

        var depo = new Depo { TesisId = _tesisId, Kod = $"DEP-{_uniqueSuffix}"[..40], Ad = $"Depo {_uniqueSuffix}", AktifMi = true };
        dbContext.Depolar.Add(depo);
        await dbContext.SaveChangesAsync();
        _depoId = depo.Id;

        var tasinirKod = new TasinirKod { TamKod = $"TK-{_uniqueSuffix}"[..40], Kod = "TK", Ad = "Tasinir Kod", DuzeyNo = 1, AktifMi = true };
        dbContext.TasinirKodlar.Add(tasinirKod);
        await dbContext.SaveChangesAsync();

        var tasinirKart = new TasinirKart
        {
            TesisId = _tesisId,
            TasinirKodId = tasinirKod.Id,
            StokKodu = $"STK-{_uniqueSuffix}"[..40],
            Ad = "Test Tasinir",
            Birim = "Adet",
            TakipTipi = "Yok",
            AktifMi = true,
            KdvOrani = 0m
        };
        dbContext.TasinirKartlar.Add(tasinirKart);
        await dbContext.SaveChangesAsync();
        _tasinirKartId = tasinirKart.Id;

        var kantin = new Kantin
        {
            TesisId = _tesisId,
            DepoId = _depoId,
            Kod = $"KNT-{_uniqueSuffix}"[..40],
            Ad = "Test Kantin",
            AktifMi = true
        };
        dbContext.Kantinler.Add(kantin);
        await dbContext.SaveChangesAsync();
        _kantinId = kantin.Id;

        var satisNoktasi = new KantinSatisNoktasi
        {
            KantinId = _kantinId,
            Kod = $"ANA-{_uniqueSuffix}"[..40],
            Ad = "Ana Nokta",
            VarsayilanMi = true,
            AktifMi = true
        };
        dbContext.KantinSatisNoktalari.Add(satisNoktasi);
        await dbContext.SaveChangesAsync();

        var kantinUrun = new KantinUrun
        {
            KantinId = _kantinId,
            TasinirKartId = tasinirKart.Id,
            Barkod = $"BRK-{_uniqueSuffix}"[..40],
            SatisFiyati = 50m,
            AktifMi = true
        };
        dbContext.KantinUrunler.Add(kantinUrun);
        await dbContext.SaveChangesAsync();

        var satis = new KantinSatis
        {
            TesisId = _tesisId,
            KantinId = _kantinId,
            SatisNoktasiId = satisNoktasi.Id,
            SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            Durum = KantinSatisDurumlari.Kesinlesti,
            KesinlesmeTarihi = new DateTime(2026, 8, 24, 10, 1, 0),
            ToplamTutar = 500m,
            MatrahToplami = 500m,
            KdvToplami = 0m,
            Aciklama = $"SATIS-{_uniqueSuffix}"
        };
        dbContext.KantinSatislar.Add(satis);
        await dbContext.SaveChangesAsync();
        _kantinSatisId = satis.Id;

        var cikisHareket = new StokHareket
        {
            DepoId = _depoId,
            TasinirKartId = tasinirKart.Id,
            HareketTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            HareketTipi = StokHareketTipleri.Cikis,
            Miktar = 10m,
            BirimFiyat = 50m,
            Tutar = 500m,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = 1,
            KdvOrani = 0m,
            KdvTutari = 0m,
            MaliyetBirimFiyat = 10m,
            MaliyetTutari = 100m,
            KaynakModul = "KantinSatisSatir",
            KaynakId = 0,
            Aciklama = $"SATIS-HRK-{_uniqueSuffix}"
        };
        dbContext.StokHareketleri.Add(cikisHareket);
        await dbContext.SaveChangesAsync();

        var satisSatir = new KantinSatisSatir
        {
            KantinSatisId = _kantinSatisId,
            KantinUrunId = kantinUrun.Id,
            TasinirKartId = tasinirKart.Id,
            Miktar = 10m,
            BirimSatisFiyati = 50m,
            KdvOrani = 0m,
            Matrah = 500m,
            KdvTutari = 0m,
            ToplamTutar = 500m,
            StokHareketId = cikisHareket.Id,
            StokKodu = $"STK-{_uniqueSuffix}"[..40],
            UrunAdi = "Test Urun",
            Birim = "Adet",
            TakipTipi = "Yok"
        };
        dbContext.KantinSatisSatirlari.Add(satisSatir);
        await dbContext.SaveChangesAsync();
        _satisSatirId = satisSatir.Id;

        cikisHareket.KaynakId = satisSatir.Id;
        await dbContext.SaveChangesAsync();

        _iadeId = await CreateIadeAsync(dbContext, 2m);
    }

    private async Task<int> CreateIadeAsync(StysAppDbContext dbContext, decimal miktar)
    {
        var iade = new KantinSatisIade
        {
            TesisId = _tesisId,
            KantinSatisId = _kantinSatisId,
            IadeTarihi = new DateTime(2026, 8, 24, 11, 0, 0),
            Durum = KantinSatisIadeDurumlari.Taslak,
            FinansalIadeDurumu = KantinSatisIadeFinansalDurumlari.Bekliyor,
            Aciklama = $"IADE-{_uniqueSuffix}-{Guid.NewGuid():N}"
        };
        dbContext.KantinSatisIadeleri.Add(iade);
        await dbContext.SaveChangesAsync();

        dbContext.KantinSatisIadeSatirlari.Add(new KantinSatisIadeSatir
        {
            KantinSatisIadeId = iade.Id,
            KantinSatisSatirId = _satisSatirId,
            Miktar = miktar,
            TasinirKartId = _tasinirKartId,
            StokKodu = $"STK-{_uniqueSuffix}"[..40],
            UrunAdi = "Test Urun",
            Birim = "Adet",
            TakipTipi = "Yok",
            BirimSatisFiyati = 50m,
            KdvOrani = 0m
        });
        await dbContext.SaveChangesAsync();

        return iade.Id;
    }

    private sealed class KantinSatisSelectBarrierInterceptor(SemaphoreSlim gate, CountdownEvent hazir) : DbCommandInterceptor
    {
        private bool _tetiklendi;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_tetiklendi
                && command.CommandText.Contains("KantinSatislar", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                _tetiklendi = true;
                hazir.Signal();
                await gate.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static StysAppDbContext CreateDbContextWithInterceptor(IInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new StysAppDbContext(
            options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentTenantAccessor());
    }

    private static KantinSatisIadeService CreateIadeService(StysAppDbContext dbContext)
        => new(
            dbContext,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new IntegrationFakeStokHareketService(dbContext),
            new IntegrationFakeRestoreService());

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(KantinProfile).Assembly);
            cfg.AddMaps(typeof(KantinSatisProfile).Assembly);
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static KantinSatisService CreateSatisService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        return new KantinSatisService(
            dbContext,
            new KantinSatisRepository(dbContext, mapper),
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            new IntegrationFakeStokHareketService(dbContext),
            null!, // ITahsilatOdemeBelgesiService: odemesiz satışta (bu testlerde) çağrılmaz.
            null!, // IMuhasebeFisService: MuhasebeFisId null olduğunda (bu testlerde) çağrılmaz.
            new IntegrationFakeRestoreService(),
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            mapper);
    }

    private static async Task<(bool Basarili, string? Hata)> SafeCallAsync(Func<Task> action)
    {
        try
        {
            await action();
            return (true, null);
        }
        catch (BaseException ex)
        {
            return (false, ex.Message);
        }
    }

    private sealed class IntegrationFakeRestoreService : IStokMaliyetKatmaniRestoreService
    {
        public Task RestoreLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto reversalMovement, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<StokMaliyetRestorePlan?> PlanPartialRestoreAsync(int originalMovementId, decimal alreadyRestoredQuantity, decimal returnQuantity, CancellationToken cancellationToken = default)
            => Task.FromResult<StokMaliyetRestorePlan?>(null);
        public Task RestorePlannedLayersAsync(StokMaliyetRestorePlan plan, StokHareketDto iadeMovement, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class IntegrationFakeStokHareketService(StysAppDbContext dbContext) : IStokHareketService
    {
        public async Task<StokHareketDto> AddWithinCurrentTransactionAsync(StokHareketDto dto, CancellationToken cancellationToken = default)
        {
            var entity = new StokHareket
            {
                DepoId = dto.DepoId,
                TasinirKartId = dto.TasinirKartId,
                HareketTarihi = dto.HareketTarihi,
                HareketTipi = dto.HareketTipi,
                Miktar = dto.Miktar,
                BirimFiyat = dto.BirimFiyat,
                Tutar = dto.Tutar,
                BelgeTarihi = dto.BelgeTarihi,
                Aciklama = dto.Aciklama,
                KaynakModul = dto.KaynakModul,
                KaynakId = dto.KaynakId,
                Durum = dto.Durum,
                KdvUygulamaTipi = dto.KdvUygulamaTipi,
                KdvOrani = dto.KdvOrani,
                KdvTutari = dto.KdvTutari,
                MaliyetBirimFiyat = dto.MaliyetBirimFiyat,
                MaliyetTutari = dto.MaliyetTutari,
                StokLotId = dto.StokLotId,
                StokSeriId = dto.StokSeriId
            };
            dbContext.StokHareketleri.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            dto.Id = entity.Id;
            return dto;
        }

        public Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<StokDegerlemeDto>> GetStokDegerlemeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<StokLotBakiyeDto>> GetLotBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<StokSeriBakiyeDto>> GetSeriBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StokHareketDto>> CreateTransferAsync(StokTransferRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StokHareketDto>> CreateTransferWithinCurrentTransactionAsync(StokTransferRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TransferIptalAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<StokHareketDto>> GetAllAsync(Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotSupportedException();
        public Task<StokHareketDto?> GetByIdAsync(int id, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<StokHareketDto>> GetPagedAsync(PagedRequest request, Expression<Func<StokHareket, bool>>? predicate = null, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null, Func<IQueryable<StokHareket>, IOrderedQueryable<StokHareket>>? orderBy = null) => throw new NotSupportedException();
        public Task<StokHareketDto> AddAsync(StokHareketDto dto) => throw new NotSupportedException();
        public Task<StokHareketDto> UpdateAsync(StokHareketDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<StokHareketDto>> WhereAsync(Expression<Func<StokHareket, bool>> predicate, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(Expression<Func<StokHareket, bool>> predicate, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotSupportedException();
    }

    [IntegrationFact]
    public async Task AyniIadeIkiEsZamanliFinalize_YalnizcaTekStokHareketiVeIkinciIdempotent()
    {
        await SeedAsync();

        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var serviceA = CreateIadeService(ctx1);
        var serviceB = CreateIadeService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => serviceA.KesinlestirAsync(_iadeId, CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(() => serviceB.KesinlestirAsync(_iadeId, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(15));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede UPDLOCK SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        // Her iki çağrı da başarılı olur (idempotent) — ama yalnızca TEK stok hareketi oluşur.
        Assert.True(aBasarili, $"A başarılı olmalı: {aHata}");
        Assert.True(bBasarili, $"B başarılı olmalı: {bHata}");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var iadeHareketleri = await verifyCtx.StokHareketleri
            .IgnoreQueryFilters()
            .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir)
            .ToListAsync();
        Assert.Single(iadeHareketleri);

        var iadeDb = await verifyCtx.KantinSatisIadeleri.AsNoTracking().SingleAsync(x => x.Id == _iadeId);
        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, iadeDb.Durum);
        Assert.True(iadeDb.KesinlesmeTarihi.HasValue);
    }

    [IntegrationFact]
    public async Task IkiFarkliTaslakIade_EsZamanliFinalize_ToplamMiktarSatisMiktariniAsamaz()
    {
        await SeedAsync();

        // Aynı source satır için iki FARKLI Taslak iade (6 + 6 > satılan 10).
        int iadeAId;
        int iadeBId;
        await using (var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            iadeAId = await CreateIadeAsync(setupCtx, 6m);
            iadeBId = await CreateIadeAsync(setupCtx, 6m);
        }

        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var serviceA = CreateIadeService(ctx1);
        var serviceB = CreateIadeService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => serviceA.KesinlestirAsync(iadeAId, CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(() => serviceB.KesinlestirAsync(iadeBId, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(15));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede UPDLOCK SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        // 6 + 6 > satılan 10 → tam olarak BİRİ başarılı olur, diğeri kümülatif sınırı görüp reddedilir.
        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");
        Assert.Contains("Kümülatif iade miktarı satış miktarını aşamaz", (aBasarili ? bHata : aHata)!);

        // Kesinleşmiş toplam tam 6 olmalı.
        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var kesinlesmisToplam = await verifyCtx.KantinSatisIadeSatirlari
            .AsNoTracking()
            .Where(x =>
                x.KantinSatisSatirId == _satisSatirId
                && x.KantinSatisIade != null
                && x.KantinSatisIade.Durum == KantinSatisIadeDurumlari.Kesinlesti)
            .SumAsync(x => (decimal?)x.Miktar) ?? 0m;

        Assert.Equal(6m, kesinlesmisToplam);
    }

    [IntegrationFact]
    public async Task SatisIptaliIleIadeFinalize_EsZamanli_YalnizcaBiriBasarili()
    {
        await SeedAsync();

        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new KantinSatisSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var iptalService = CreateSatisService(ctx1);
        var iadeService = CreateIadeService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => iptalService.IptalEtAsync(_kantinSatisId, "İptal")));
        var taskB = Task.Run(() => SafeCallAsync(() => iadeService.KesinlestirAsync(_iadeId, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(15));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede UPDLOCK SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (iptalBasarili, iptalHata) = await taskA;
        var (iadeBasarili, iadeHata) = await taskB;

        // Yalnız BİR workflow başarılı olur.
        Assert.True(iptalBasarili ^ iadeBasarili,
            $"Tam olarak biri başarılı olmalı. İptal={iptalBasarili} ({iptalHata}), İade={iadeBasarili} ({iadeHata})");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisDb = await verifyCtx.KantinSatislar.AsNoTracking().SingleAsync(x => x.Id == _kantinSatisId);
        var iadeDb = await verifyCtx.KantinSatisIadeleri.AsNoTracking().SingleAsync(x => x.Id == _iadeId);

        var iptalHareketVar = await verifyCtx.StokHareketleri
            .IgnoreQueryFilters()
            .AnyAsync(x => x.KaynakModul == "KantinSatisIptal");
        var iadeHareketVar = await verifyCtx.StokHareketleri
            .IgnoreQueryFilters()
            .AnyAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);

        if (iptalBasarili)
        {
            // Satış iptal olduysa iade Kesinlesti olmamalı; full reversal + partial return birlikte olmamalı.
            Assert.Equal(KantinSatisDurumlari.IptalEdildi, satisDb.Durum);
            Assert.Equal(KantinSatisIadeDurumlari.Taslak, iadeDb.Durum);
            Assert.True(iptalHareketVar);
            Assert.False(iadeHareketVar);
            Assert.False(iadeBasarili);
            Assert.Contains("kesinleşmiş satışlardan iade", iadeHata);
        }
        else
        {
            // İade Kesinlesti olduysa satış iptal edilmemeli; full reversal + partial return birlikte olmamalı.
            Assert.Equal(KantinSatisDurumlari.Kesinlesti, satisDb.Durum);
            Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, iadeDb.Durum);
            Assert.True(iadeHareketVar);
            Assert.False(iptalHareketVar);
            Assert.False(iptalBasarili);
            Assert.Contains("kesinleşmiş ürün iadesi bulunduğundan", iptalHata);
        }
    }
}
