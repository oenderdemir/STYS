using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeSnapshotUblHazirlikIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-UBL";
    private const string SeriKodu = "EBH";
    private readonly DateTime _vadeTarihi = new(2026, 4, 15);

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriHesapId;
    private int _tedarikciHesapId;
    private int _musteriKartId;
    private int _tedarikciKartId;
    private string _kurumVergiDairesi = string.Empty;
    private string _kurumAdres = string.Empty;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;
        _kurumVergiDairesi = kurum.VergiDairesi!;
        _kurumAdres = kurum.Adres!;

        // Faz 2B.11.1 - bu dosya artık EBelgeUblOptions.Enabled=true ile çalıştığından (bkz.
        // CreateService XML doc'u), kesim öncesi UBL kapısı (IEBelgeUblPreCutValidator) TAM olarak
        // değerlendirilir - satıcı yapısal adresi (ilçe/il) ZORUNLUDUR. Bu, dosyanın KENDİ
        // VergiNo/VergiDairesi/Adres eksik senaryolarını (EnsureUblHazirlikKaynaklari, pre-cut
        // validator'DAN ÖNCE çalışır) ETKİLEMEZ - o kontroller Ilce/Il'DAN BAĞIMSIZDIR.
        kurum.Ilce = "Kadıköy";
        kurum.Il = "İstanbul";
        await dbContext.SaveChangesAsync();

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
        musteriKart.VergiNoTckn = "1111111111";
        // Faz 2B.11.1 - CariKartTipleri.Musteri "gerçek kişi" olarak ele alınır - kesim öncesi UBL
        // kapısı ayrı ad/soyad + yapısal adres (ilçe/il) ister (VergiNoTckn zaten yukarıda var).
        musteriKart.Ad = "SnapshotUbl";
        musteriKart.Soyad = "Musteri " + _uniqueSuffix;
        musteriKart.Ilce = "Beşiktaş";
        musteriKart.Il = "İstanbul";
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "2222222222";
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
            SeriKodu = SeriKodu,
            SonNumara = 0,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private async Task EnsureMuhasebeFisIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        var muhasebeFisId = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == satisBelgesiId)
            .Select(x => x.MuhasebeFisId)
            .SingleAsync();

        Assert.True(muhasebeFisId.HasValue);
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

    /// <summary>Faz 2B.11.1 - bkz. EBelgeFaz1IntegrationTests.SafeKesimZamani XML doc'u.</summary>
    private static readonly DateTimeOffset SafeKesimZamani = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FakeTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
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
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger(),
            timeProvider: new FakeTimeProvider(SafeKesimZamani),
            // Faz 2B.11.1 - bu dosyanın testleri snapshot İÇERİĞİNİ (VergiNo/Adres/ParaBirimi/Kur
            // gibi alanların doğru yazıldığını) doğrular, UBL feature flag semantiğini test ETMEZ -
            // `Enabled=true`, EBelgeKaydi/snapshot/outbox'ın (SeedKurumIlTesisAsync'in seed ettiği
            // aktif DogrudanGib test politikasıyla) KOŞULSUZ oluştuğu ÖNCEKİ davranışı KORUR;
            // aksi halde yeni runtime fail-closed guard'ı (bkz.
            // SatisBelgesiService.EnsureUblFeatureAcikYerelUblGerekliyse) bu testleri BOZAR.
            eBelgeUblOptions: Options.Create(new EBelgeUblOptions { Enabled = true }),
            // Faz 2B.10 - bkz. SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService XML doc'u.
            kurumPolitikaServisi: EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext));
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

    private CreateSatisBelgesiRequest BuildSatisBelgesiRequest()
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-UBL-{Guid.NewGuid():N}", 40),
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 9, 20),
            VadeTarihi = _vadeTarihi,
            MusteriUnvan = "Snapshot Musteri " + _uniqueSuffix,
            MusteriAdSoyad = "Snapshot Musteri Ad Soyad " + _uniqueSuffix,
            MusteriVergiNo = "1234567890",
            MusteriTcKimlikNo = null,
            MusteriVergiDairesi = "Musteri Vergi Dairesi " + _uniqueSuffix,
            MusteriAdres = "Musteri Adres " + _uniqueSuffix,
            MusteriEposta = "snapshot@example.com",
            MusteriTelefon = "05000000000",
            KurumsalMi = true,
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

    private async Task<int> PrepareReadyInvoiceAsync()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest(), CancellationToken.None);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);

        await EnsureMuhasebeFisIdAsync(created.Id.Value);
        return created.Id.Value;
    }

    private async Task AssertNoCutArtifactsAsync(int satisBelgesiId, int expectedSayac)
    {
        await using var verifyCtx = CreateDbContext();

        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .SingleAsync(x => x.Id == satisBelgesiId);

        Assert.Null(belge.ResmiFaturaNo);
        Assert.Null(belge.EBelgeUuid);
        Assert.Null(belge.FaturaKesimTarihi);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, belge.FaturalamaDurumu);

        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == satisBelgesiId));
        Assert.False(await verifyCtx.EBelgeSnapshots.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId));
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == satisBelgesiId));

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        Assert.Equal(expectedSayac, sayacSonra.SonNumara);
    }

    private async Task<(SatisBelgesiDto Cut, EBelgeSnapshot Snapshot)> CutReadyInvoiceAsync()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var cut = await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);

        await using var snapshotCtx = CreateDbContext();
        var snapshot = await snapshotCtx.EBelgeSnapshots
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == belgeId);

        return (cut, snapshot);
    }

    [IntegrationFact]
    public async Task FaturaKesildigindeKurumVergiDairesiVeHukukiAdresiSnapshotaAynenYazilir()
    {
        var (cut, snapshot) = await CutReadyInvoiceAsync();

        Assert.Equal("TRY", cut.ParaBirimi);
        Assert.Equal(1m, cut.Kur);

        using var document = JsonDocument.Parse(snapshot.CanonicalJson);
        var kurum = document.RootElement.GetProperty("kurum");

        Assert.Equal(_kurumVergiDairesi, GetNullableString(kurum, "vergiDairesi"));
        Assert.Equal(_kurumAdres, GetNullableString(kurum, "adres"));
    }

    [IntegrationFact]
    public async Task FaturaKesildigindeParaBirimiTryVeKurBirSnapshotaYazilir()
    {
        var (_, snapshot) = await CutReadyInvoiceAsync();

        using var document = JsonDocument.Parse(snapshot.CanonicalJson);
        var odeme = document.RootElement.GetProperty("odeme");

        Assert.Equal("TRY", GetNullableString(odeme, "paraBirimi"));
        Assert.Equal(1m, GetRequiredDecimal(odeme, "kur"));
    }

    [IntegrationFact]
    public async Task OdemeTuruVarsayimiUretilmezVeVadeTarihiKalinir()
    {
        var (_, snapshot) = await CutReadyInvoiceAsync();

        using var document = JsonDocument.Parse(snapshot.CanonicalJson);
        var odeme = document.RootElement.GetProperty("odeme");

        Assert.Null(GetNullableString(odeme, "odemeTuru"));
        Assert.Equal(_vadeTarihi, GetRequiredDateTime(odeme, "vadeTarihi"));
    }

    [IntegrationFact]
    public async Task KurumVergiNumarasiEksikseKesimAtomikOlarakReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.Kurumlar
                .Where(x => x.Id == _kurumId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.VergiNo, (string?)null));
        }

        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(() => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));
        Assert.Contains("vergi numarası", ex.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    [IntegrationFact]
    public async Task KurumVergiDairesiEksikseKesimAtomikOlarakReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.Kurumlar
                .Where(x => x.Id == _kurumId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.VergiDairesi, (string?)null));
        }

        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(() => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));
        Assert.Contains("vergi dairesi", ex.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    [IntegrationFact]
    public async Task KurumHukukiAdresiEksikseKesimAtomikOlarakReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.Kurumlar
                .Where(x => x.Id == _kurumId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Adres, (string?)null));
        }

        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(() => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));
        Assert.Contains("hukuki adres", ex.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    [IntegrationFact]
    public async Task ParaBirimiTryDisindaOldugundaKesimAtomikOlarakReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgeleri
                .Where(x => x.Id == belgeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ParaBirimi, "USD")
                    .SetProperty(x => x.Kur, 1m));
        }

        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(() => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));
        Assert.Contains("TRY para birimi", ex.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    [IntegrationFact]
    public async Task KurBirDisindaOldugundaKesimAtomikOlarakReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync();

        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgeleri
                .Where(x => x.Id == belgeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ParaBirimi, "TRY")
                    .SetProperty(x => x.Kur, 1.25m));
        }

        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(() => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));
        Assert.Contains("kur 1", ex.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    private async Task<int> GetSayacOnceAsync()
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu)
            .Select(x => x.SonNumara)
            .SingleAsync();
    }

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? GetNullableString(JsonElement parent, string propertyName)
    {
        var property = parent.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }

    private static decimal GetRequiredDecimal(JsonElement parent, string propertyName)
        => parent.GetProperty(propertyName).GetDecimal();

    private static DateTime GetRequiredDateTime(JsonElement parent, string propertyName)
        => parent.GetProperty(propertyName).GetDateTime();

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
