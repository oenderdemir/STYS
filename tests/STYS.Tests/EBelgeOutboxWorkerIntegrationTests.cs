using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8 görev md.18 "Gerçek integration" - `EBelgeOutboxWorker`'ı, GERÇEK SQL Server + GERÇEK
/// renderer + GERÇEK Java Saxon sidecar + GERÇEK (test) RSA sertifikasıyla, MEVCUT
/// claim/lease/işleme servislerinin TAMAMEN GERÇEK implementasyonlarına karşı çalıştırır (mock
/// YOKTUR). Worker'ın kendi DI container'ı, `backend/Program.cs`'teki registration'ların BİREBİR
/// aynısını (Program.cs'i ÇAĞIRMADAN, test-only bir `ServiceCollection` üzerinde) yeniden kurar -
/// yalnız test sertifika sağlayıcısı/güven politikası GERÇEK üretim registration'larının YERİNE
/// geçer (bkz. görev md.16, "test certificate provider production DI'a EKLENMEMELİ" - burada
/// KASITLI olarak TEST-ONLY bir container'dır, Program.cs HİÇ DEĞİŞTİRİLMEZ).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("Dependency", "SqlServer")]
[Trait("Dependency", "JavaSidecar")]
[Trait("Dependency", "Cryptography")]
public class EBelgeOutboxWorkerIntegrationTests : IAsyncLifetime, IClassFixture<SchematronSidecarProcessFixture>
{
    private const string TestMarker = "EBO-2B8";

    private readonly SchematronSidecarProcessFixture _sidecarFixture;
    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public EBelgeOutboxWorkerIntegrationTests(SchematronSidecarProcessFixture sidecarFixture)
    {
        _sidecarFixture = sidecarFixture;
    }

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.Add(musteriHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;

        // Faz 2B.10 - bu testler EBelgeKaydi/outbox'ı SatisBelgesiService'in fatura-kesme akışını
        // (dolayısıyla kurum politikası kararını) KULLANMADAN DOĞRUDAN seed eder - aktif bir
        // test-only politika seed edilir (bkz. EBelgeKurumPolitikaTestSupport).
        await EBelgeKurumPolitikaTestSupport.SeedAktifDogrudanGibPolitikaAsync(dbContext, _kurumId);
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [muhasebe].[EBelgeArtifactlari] WHERE [KurumId] = {_kurumId}");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [muhasebe].[EBelgeOutboxMesajlari] WHERE [KurumId] = {_kurumId}");

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    private async Task<int> CreateSatisBelgesiIdAsync(StysAppDbContext dbContext)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 7, 1),
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satiri",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

        var created = await service.CreateAsync(request);
        return created.Id!.Value;
    }

    /// <summary>Bkz. `EBelgeArtefaktOlusturmaServiceIntegrationTests.SeedEBelgeKaydiWithV2SnapshotAsync` İLE AYNI desen.</summary>
    private async Task<int> SeedEBelgeKaydiWithV2SnapshotAsync(StysAppDbContext dbContext)
    {
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);

        var v2Snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2Snapshot, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(utf8Bytes));
        var json = Encoding.UTF8.GetString(utf8Bytes);

        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.SnapshotHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        dbContext.EBelgeSnapshots.Add(new EBelgeSnapshot
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            BelgeVersiyonu = 1,
            SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
            CanonicalJson = json,
            CanonicalSha256 = hash,
        });
        await dbContext.SaveChangesAsync();

        await EBelgeKurumPolitikaTestSupport.SeedEBelgeKarariAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydi.Id);

        return eBelgeKaydi.Id;
    }

    /// <summary>Bkz. `EBelgeUblImzalamaServiceIntegrationTests.SeedUnsignedArtifactAsync` İLE AYNI desen - GERÇEK renderer + GERÇEK sidecar ile üretilmiş GEÇERLİ bir Unsigned UBL artefaktı.</summary>
    private async Task<(int eBelgeKaydiId, EBelgeArtifact unsignedArtifact)> SeedUnsignedArtifactAsync(StysAppDbContext dbContext)
    {
        var satisBelgesiId = await CreateSatisBelgesiIdAsync(dbContext);
        var eBelgeKaydi = new EBelgeKaydi
        {
            KurumId = _kurumId,
            SatisBelgesiId = satisBelgesiId,
            EBelgeUuid = Guid.NewGuid().ToString("D"),
            EBelgeKanali = EBelgeKanali.EArsiv,
            Durum = EBelgeKaydiDurumu.UnsignedUblHazir,
        };
        dbContext.EBelgeKayitlari.Add(eBelgeKaydi);
        await dbContext.SaveChangesAsync();

        var renderer = RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!);
        var renderSonucu = await renderer.RenderAsync(EBelgeUblRendererTestVerisi.GecerliSnapshot(), CancellationToken.None);

        var unsigned = new EBelgeArtifact
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydi.Id,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = renderSonucu.KuralSetiKimligi,
            SnapshotSchemaVersion = int.Parse(EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion),
            KaynakSnapshotSha256 = new string('a', 64),
            ArtifactSha256 = renderSonucu.UnsignedUblSha256,
            Icerik = renderSonucu.UnsignedUblUtf8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = "worker-test-unsigned.xml",
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        };
        dbContext.EBelgeArtifactlari.Add(unsigned);
        await dbContext.SaveChangesAsync();

        await EBelgeKurumPolitikaTestSupport.SeedEBelgeKarariAsync(dbContext, _kurumId, satisBelgesiId, eBelgeKaydi.Id);

        return (eBelgeKaydi.Id, unsigned);
    }

    private async Task SeedOutboxMesajiAsync(StysAppDbContext dbContext, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru)
    {
        dbContext.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = isTuru,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeSigningActivationGate : IEBelgeSigningActivationGate
    {
        public static readonly FakeSigningActivationGate Kapali = new(false);
        public static readonly FakeSigningActivationGate Acik = new(true);

        private readonly bool _sonuc;
        private FakeSigningActivationGate(bool sonuc) => _sonuc = sonuc;
        public bool ShouldCreateSigningMessage() => _sonuc;
        public bool CanSignNow() => _sonuc;
    }

    private static EBelgeProcessingOptions HizliWorkerOptions(int leaseDurationSeconds = 60) => new()
    {
        Enabled = true,
        NotBeforeLocalDate = "2020-01-01",
        TimeZoneId = "Europe/Istanbul",
        PollIntervalSeconds = 1,
        IdlePollIntervalSeconds = 1,
        BatchSize = 5,
        LeaseDurationSeconds = leaseDurationSeconds,
        MaxParallelism = 1,
        ShutdownGracePeriodSeconds = 5,
    };

    /// <summary>
    /// `backend/Program.cs`'teki GERÇEK e-belge outbox/signing/worker registration'larının
    /// BİREBİR AYNISI (test-only bir `ServiceCollection` üzerinde) - yalnız sertifika sağlayıcısı/
    /// güven politikası TEST double'ları İLE (bkz. görev md.16 - production DI HİÇ DEĞİŞTİRİLMEZ,
    /// bu TAMAMEN AYRI bir container'dır).
    /// </summary>
    private ServiceProvider BuildWorkerContainer(EBelgeProcessingOptions options, bool signingGateAcik)
    {
        if (_sidecarFixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_sidecarFixture.AtlamaNedeni}");
        }

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddScoped(_ => SatisBelgesiMuhasebeTestSupport.CreateDbContext());
        services.AddScoped<IEBelgeOutboxClaimLeaseService, EBelgeOutboxClaimLeaseService>();
        services.AddScoped<IEBelgeOutboxLeaseTransitionService, EBelgeOutboxLeaseTransitionService>();
        services.AddScoped<IEBelgeOutboxRetryPolicy, EBelgeOutboxRetryPolicy>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEBelgeCanonicalSnapshotV2Reader, EBelgeCanonicalSnapshotV2Reader>();
        services.AddSingleton<IEBelgeUblRenderer>(_ => RealRendererTestSupport.CreateRealRenderer(_sidecarFixture.BaseUrl!));
        services.AddSingleton<IEBelgeSigningActivationGate>(_ => signingGateAcik ? FakeSigningActivationGate.Acik : FakeSigningActivationGate.Kapali);
        // Faz 2B.10 - TEST-ONLY yetenek sağlayıcısı (DogrudanGib TAM operasyonel) - production
        // DI'da (backend/Program.cs) HİÇBİR ZAMAN kaydedilmez, yalnız bu gerçek XAdES/SignedReady
        // E2E test container'ında (bkz. EBelgeKurumPolitikaTestSupport).
        services.AddSingleton<IEBelgeYontemYetenekSaglayici>(EBelgeTestYontemYetenekSaglayici.Instance);
        services.AddScoped<IEBelgeKurumPolitikaServisi, EBelgeKurumPolitikaServisi>();
        // Faz 2B.10.2 - EBelgeArtefaktOlusturmaService/EBelgeUblImzalamaService'in ikisi de ARTIK
        // bu guard'ı zorunlu bağımlılık olarak alır (bkz. backend/Program.cs'teki GERÇEK kayıt) -
        // bu manuel test DI container'ı da AYNI kaydı taşımalıdır.
        services.AddScoped<IEBelgeKurumPolitikaTransactionGuard, EBelgeKurumPolitikaTransactionGuard>();
        services.AddScoped<IEBelgeArtefaktOlusturmaService, EBelgeArtefaktOlusturmaService>();
        services.AddScoped<IEBelgeOutboxIsTuruHandler, EBelgeArtefaktOlusturOutboxHandler>();

        services.AddSingleton<IEBelgeImzaKimligiSaglayici, EBelgeTestSertifikaSaglayici>();
        services.AddSingleton<IEBelgeSertifikaGuvenValidatoru, EBelgeTestSertifikaGuvenPolicy>();
        services.AddSingleton<IEBelgeXmlImzalayici, EBelgeXmlImzalayici>();
        services.AddSingleton<IEBelgeXmlImzaDogrulayici, EBelgeXmlImzaDogrulayici>();
        services.AddSingleton(EBelgeUblRendererTestVerisi.KuralSetiYukle());
        services.AddSingleton<IEBelgeUblXsdValidator, EBelgeUblXsdValidator>();
        services.AddHttpClient<IEBelgeSchematronValidator, SaxonSidecarEBelgeSchematronValidator>(client =>
        {
            client.BaseAddress = new Uri(_sidecarFixture.BaseUrl!);
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IEBelgeUblImzalamaService, EBelgeUblImzalamaService>();
        services.AddScoped<IEBelgeOutboxIsTuruHandler, EBelgeUblImzalaOutboxHandler>();

        services.AddScoped<IEBelgeOutboxMesajIslemeService, EBelgeOutboxMesajIslemeService>();

        services.AddSingleton(Options.Create(options));
        services.AddSingleton<IEBelgeProcessingActivationGate, EBelgeProcessingActivationGate>();
        services.AddSingleton<IEBelgeOutboxWorkerDelay, TimeProviderEBelgeOutboxWorkerDelay>();
        services.AddSingleton<IEBelgeOutboxWorkerMetrics, EBelgeOutboxWorkerMetrics>();
        services.AddSingleton<IEBelgeOutboxWorkerHealthState, EBelgeOutboxWorkerHealthState>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static EBelgeOutboxWorker CreateWorker(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        provider.GetRequiredService<IEBelgeProcessingActivationGate>(),
        provider.GetRequiredService<IEBelgeOutboxWorkerMetrics>(),
        provider.GetRequiredService<IEBelgeOutboxWorkerHealthState>(),
        provider.GetRequiredService<IEBelgeOutboxWorkerDelay>(),
        provider.GetRequiredService<IOptions<EBelgeProcessingOptions>>(),
        provider.GetRequiredService<ILogger<EBelgeOutboxWorker>>());

    private static async Task WaitUntilAsync(Func<Task<bool>> kosul, TimeSpan? zamanAsimi = null)
    {
        var sinir = zamanAsimi ?? TimeSpan.FromSeconds(30);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!await kosul())
        {
            if (sw.Elapsed > sinir)
            {
                throw new TimeoutException("Beklenen DB durumu zaman aşımı içinde gerçekleşmedi.");
            }

            await Task.Delay(150);
        }
    }

    [IntegrationFact]
    [Trait("TestLevel", "WorkerEndToEnd")]
    public async Task GercekWorkerArtefaktOlusturMesajiniClaimEdipTamamlarVeUnsignedArtifactOlusur()
    {
        int eBelgeKaydiId;
        await using (var seedCtx = CreateDbContext())
        {
            eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
            await SeedOutboxMesajiAsync(seedCtx, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);
        }

        await using var provider = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: false);
        var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var verifyCtx = CreateDbContext();
                var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
                return outbox.Durum == EBelgeOutboxDurumu.Tamamlandi;
            });
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var finalCtx = CreateDbContext();
        Assert.True(await finalCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned));
        var kayit = await finalCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.UnsignedUblHazir, kayit.Durum);
    }

    [IntegrationFact]
    [Trait("TestLevel", "WorkerEndToEnd")]
    public async Task GercekWorkerUblImzalaMesajiniClaimEdipTamamlarVeSignedReadyArtifactOlusur()
    {
        int eBelgeKaydiId;
        await using (var seedCtx = CreateDbContext())
        {
            (eBelgeKaydiId, _) = await SeedUnsignedArtifactAsync(seedCtx);
            await SeedOutboxMesajiAsync(seedCtx, eBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala);
        }

        // Faz 2B.10.2 görev md.5/md.6 - global signing gate ARTIK yalnız mesaj-OLUŞTURMA anında
        // değil, İMZALAMA COMMIT'İNDEN ÖNCE de (hem handler başındaki erken kontrolde hem de
        // commit-öncesi ikinci kontrolde) GERÇEKTEN uygulanır - bu yüzden bu testin (amacı: worker
        // GERÇEKTEN imzalayıp SignedReady üretir) gate'i AÇIK kurması GEREKİR. Gate KAPALIYKEN
        // kuyruklu mesajın işlenmeye HİÇ başlamayacağı/SignedReady ÜRETMEYECEĞİ AYRICA, doğrudan
        // `EBelgeUblImzalamaServiceIntegrationTests`'te (`SigningGateKapaliykenKuyruktakiMesajHicIslenmeyeBaslamazVeSignedReadyYazilmaz`)
        // test edilir - burada TEKRAR EDİLMEZ.
        await using var provider = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: true);
        var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var verifyCtx = CreateDbContext();
                var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
                return outbox.Durum == EBelgeOutboxDurumu.Tamamlandi;
            });
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var finalCtx = CreateDbContext();
        Assert.True(await finalCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady));
        var kayit = await finalCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
        Assert.Equal(EBelgeKaydiDurumu.SignedReady, kayit.Durum);
    }

    [IntegrationFact]
    [Trait("TestLevel", "ReleaseGate")]
    [Trait("CriticalInvariant", "WorkerEndToEndSignedReady")]
    public async Task GercekWorkerArtefaktOlusturdanUblImzalayaZincirlemeTamamlarUctanUcaSignedReadyUretir()
    {
        // Faz 2B.8 görev md.18 senaryo 44-47 - TEK bir worker, ÖNCE ArtefaktOlustur mesajını
        // claim edip GERÇEK sidecar ile Unsigned artefaktı ÜRETİR (signing gate AÇIK olduğundan
        // AYNI atomik işlem KENDİLİĞİNDEN bir UblImzala mesajı da ZİNCİRLER), SONRA (bir sonraki
        // polling turunda) O mesajı da claim edip GERÇEK test sertifikasıyla İMZALAR - TAMAMEN
        // worker'ın KENDİ polling döngüsü ÜZERİNDEN, uçtan uca.
        int eBelgeKaydiId;
        await using (var seedCtx = CreateDbContext())
        {
            eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
            await SeedOutboxMesajiAsync(seedCtx, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);
        }

        await using var provider = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: true);
        var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var verifyCtx = CreateDbContext();
                var kayit = await verifyCtx.EBelgeKayitlari.AsNoTracking().SingleAsync(x => x.Id == eBelgeKaydiId);
                return kayit.Durum == EBelgeKaydiDurumu.SignedReady;
            }, TimeSpan.FromSeconds(45));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var finalCtx = CreateDbContext();
        Assert.True(await finalCtx.EBelgeArtifactlari.AnyAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned));
        var signed = await finalCtx.EBelgeArtifactlari.AsNoTracking().SingleAsync(a => a.EBelgeKaydiId == eBelgeKaydiId && a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady);
        Assert.NotNull(signed.ImzaProfili);

        var tumOutboxMesajlari = await finalCtx.EBelgeOutboxMesajlari.AsNoTracking().Where(x => x.EBelgeKaydiId == eBelgeKaydiId).ToListAsync();
        Assert.All(tumOutboxMesajlari, m => Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, m.Durum));
    }

    [IntegrationFact]
    [Trait("TestLevel", "WorkerEndToEnd")]
    public async Task WorkerKapatilipYenidenBaslatildigindaTamamlanmisMesajTekrarIslenmez()
    {
        int eBelgeKaydiId;
        await using (var seedCtx = CreateDbContext())
        {
            eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
            await SeedOutboxMesajiAsync(seedCtx, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);
        }

        await using (var provider1 = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: false))
        {
            var worker1 = CreateWorker(provider1);
            await worker1.StartAsync(CancellationToken.None);
            try
            {
                await WaitUntilAsync(async () =>
                {
                    await using var verifyCtx = CreateDbContext();
                    var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
                    return outbox.Durum == EBelgeOutboxDurumu.Tamamlandi;
                });
            }
            finally
            {
                await worker1.StopAsync(CancellationToken.None);
            }
        }

        int artifactSayisiIlkTurdenSonra;
        await using (var araCtx = CreateDbContext())
        {
            artifactSayisiIlkTurdenSonra = await araCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        }

        // "Yeniden başlatma" - TAMAMEN YENİ bir worker/container örneği, AYNI DB'ye karşı.
        await using var provider2 = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: false);
        var worker2 = CreateWorker(provider2);
        await worker2.StartAsync(CancellationToken.None);
        await Task.Delay(2500); // birkaç polling turu boyunca çalışsın - claim edecek YENİ bir şey YOK
        await worker2.StopAsync(CancellationToken.None);

        await using var finalCtx = CreateDbContext();
        var artifactSayisiSonra = await finalCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(artifactSayisiIlkTurdenSonra, artifactSayisiSonra);

        var outbox = await finalCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(EBelgeOutboxDurumu.Tamamlandi, outbox.Durum);
    }

    [IntegrationFact]
    [Trait("TestLevel", "WorkerEndToEnd")]
    public async Task IkiInstanceAyniMesajiIsleyemezVeLeaseSuresiDolduktanSonraIkinciWorkerTamamlar()
    {
        // Faz 2B.8 görev md.5/md.18 senaryo 23-27/49 - "Instance A claim eder, Instance B aynı
        // mesajı ALAMAZ, A çöker ve lease dolar, B mesajı tekrar claim eder". "A"nın çökmesi,
        // GERÇEK claim servisiyle mesajı KISA bir lease'le claim edip SONRA HİÇ İŞLEMEDEN
        // bırakarak (worker'ın KENDİSİNİ HİÇ ÇALIŞTIRMADAN) simüle edilir - bu, "worker A çöktü"
        // senaryosunun deterministik/gerçek-zaman-beklemeyen eşdeğeridir (bkz. mevcut
        // `EBelgeArtefaktOlusturmaServiceIntegrationTests`'teki AYNI desen).
        int eBelgeKaydiId;
        await using (var seedCtx = CreateDbContext())
        {
            eBelgeKaydiId = await SeedEBelgeKaydiWithV2SnapshotAsync(seedCtx);
            await SeedOutboxMesajiAsync(seedCtx, eBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur);
        }

        int outboxMesajiId;
        await using (var claimCtx = CreateDbContext())
        {
            var instanceAClaimService = new EBelgeOutboxClaimLeaseService(claimCtx, EBelgeTestSigningActivationGate.Acik);
            var instanceAClaim = await instanceAClaimService.TryClaimNextAsync(TimeSpan.FromSeconds(2));
            Assert.NotNull(instanceAClaim);
            outboxMesajiId = instanceAClaim!.OutboxMesajiId;

            // Instance A aktif lease TUTARKEN, Instance B (AYRI bir DbContext/claim servisi) AYNI
            // mesajı ALAMAZ - kuyrukta BAŞKA claim edilebilir mesaj OLMADIĞINDAN `null` döner.
            await using var instanceBCtx = CreateDbContext();
            var instanceBClaimService = new EBelgeOutboxClaimLeaseService(instanceBCtx, EBelgeTestSigningActivationGate.Acik);
            var instanceBIlkDeneme = await instanceBClaimService.TryClaimNextAsync(TimeSpan.FromMinutes(5));
            Assert.Null(instanceBIlkDeneme);
        }

        // Instance A hiçbir sonuç YAZMADAN "çöktü" - lease'in SÜRESİ dolmasını bekle (deterministik:
        // 2 saniyelik lease SQL SYSUTCDATETIME() tabanlı olduğundan GERÇEKTEN dolmalıdır).
        await Task.Delay(TimeSpan.FromSeconds(3));

        await using var provider = BuildWorkerContainer(HizliWorkerOptions(), signingGateAcik: false);
        var instanceBWorker = CreateWorker(provider);
        await instanceBWorker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var verifyCtx = CreateDbContext();
                var outbox = await verifyCtx.EBelgeOutboxMesajlari.AsNoTracking().SingleAsync(x => x.Id == outboxMesajiId);
                return outbox.Durum == EBelgeOutboxDurumu.Tamamlandi;
            });
        }
        finally
        {
            await instanceBWorker.StopAsync(CancellationToken.None);
        }

        await using var finalCtx = CreateDbContext();
        // Faz 2B.8 görev md.5 - "aynı mesajdan duplicate artifact oluşmaz" - TAM OLARAK 1 Unsigned
        // artefaktı olmalıdır (instance A'nın YARIM kalmış denemesinden değil, yalnız instance B'nin
        // BAŞARILI tamamlamasından).
        var artifactSayisi = await finalCtx.EBelgeArtifactlari.CountAsync(a => a.EBelgeKaydiId == eBelgeKaydiId);
        Assert.Equal(1, artifactSayisi);
    }
}
