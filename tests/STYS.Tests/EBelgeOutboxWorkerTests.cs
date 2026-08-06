using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8 görev md.18 - `EBelgeOutboxWorker`'ın polling/backoff/cancellation/scope/paralellik/
/// gözlemlenebilirlik davranışını, GERÇEK SQL Server/sidecar GEREKMEDEN, tamamen SAHTE (fake)
/// `IEBelgeOutboxClaimLeaseService`/`IEBelgeOutboxMesajIslemeService` implementasyonlarıyla,
/// GERÇEK bir `IServiceScopeFactory` (küçük bir `ServiceCollection` üzerinden) kullanarak test
/// eder. Çoklu-instance/gerçek-artifact senaryoları `EBelgeOutboxWorkerIntegrationTests`'tedir.
/// </summary>
public class EBelgeOutboxWorkerTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    /// <summary>Tüm sahte servislerin PAYLAŞTIĞI, thread-safe durum/senaryo betiği.</summary>
    private sealed class SharedTestState
    {
        public readonly ConcurrentQueue<EBelgeOutboxClaimLeaseResultDto> ClaimsToOffer = new();
        public readonly ConcurrentQueue<Exception?> ClaimExceptions = new();
        public int ClaimCallCount;

        public readonly ConcurrentDictionary<int, EBelgeOutboxIslemeSonucu> ResultsByOutboxId = new();
        public Func<EBelgeOutboxClaimLeaseResultDto, CancellationToken, Task<EBelgeOutboxIslemeSonucu>>? IslemeOverride;
        public readonly ConcurrentBag<Guid> IslemeInstanceIdsGorulen = new();
        public readonly ConcurrentBag<int> IslenenOutboxIdler = new();

        public int CurrentConcurrent;
        public int MaxConcurrentObserved;
    }

    private sealed class FakeClaimLeaseService : IEBelgeOutboxClaimLeaseService
    {
        private readonly SharedTestState _state;
        public FakeClaimLeaseService(SharedTestState state) => _state = state;

        public Task<EBelgeOutboxClaimLeaseResultDto?> TryClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _state.ClaimCallCount);
            if (_state.ClaimExceptions.TryDequeue(out var ex) && ex is not null)
            {
                throw ex;
            }

            return Task.FromResult(_state.ClaimsToOffer.TryDequeue(out var claim) ? claim : null);
        }
    }

    private sealed class FakeMesajIslemeService : IEBelgeOutboxMesajIslemeService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        private readonly SharedTestState _state;
        public FakeMesajIslemeService(SharedTestState state) => _state = state;

        public async Task<EBelgeOutboxIslemeSonucu> IsleAsync(EBelgeOutboxClaimLeaseResultDto claim, CancellationToken cancellationToken = default)
        {
            _state.IslemeInstanceIdsGorulen.Add(InstanceId);
            _state.IslenenOutboxIdler.Add(claim.OutboxMesajiId);

            if (_state.IslemeOverride is not null)
            {
                return await _state.IslemeOverride(claim, cancellationToken);
            }

            return _state.ResultsByOutboxId.TryGetValue(claim.OutboxMesajiId, out var sonuc)
                ? sonuc
                : EBelgeOutboxIslemeSonucu.Tamamlandi();
        }
    }

    private sealed class FakeMetrics : IEBelgeOutboxWorkerMetrics
    {
        public int ClaimedCount;
        public readonly ConcurrentBag<(EBelgeOutboxIsTuru IsTuru, EBelgeOutboxIslemeSonucuTuru SonucTuru, TimeSpan Sure)> Results = new();
        public int PollErrorCount;
        public int InflightIncrements;
        public int InflightDecrements;

        public void RecordClaimed(EBelgeOutboxIsTuru isTuru) => Interlocked.Increment(ref ClaimedCount);
        public void RecordResult(EBelgeOutboxIsTuru isTuru, EBelgeOutboxIslemeSonucuTuru sonucTuru, TimeSpan sure) => Results.Add((isTuru, sonucTuru, sure));
        public void RecordPollError() => Interlocked.Increment(ref PollErrorCount);
        public void IncrementInflight() => Interlocked.Increment(ref InflightIncrements);
        public void DecrementInflight() => Interlocked.Increment(ref InflightDecrements);
    }

    /// <summary>Bkz. görev md.8 - `Task.Delay` yerine TEST EDİLEBİLİR bir zamanlama abstraction'ı. `BlockUntilCancelled=true` iken, her tur TAM OLARAK bir delay çağrısında BLOKE OLUR (yalnız cancellation İLE kesilir) - bu, tek bir turu deterministik biçimde İNCELEMEYİ sağlar.</summary>
    private sealed class FakeDelay : IEBelgeOutboxWorkerDelay
    {
        public readonly ConcurrentQueue<TimeSpan> RequestedDelays = new();
        public bool BlockUntilCancelled { get; set; }

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            RequestedDelays.Enqueue(delay);
            if (BlockUntilCancelled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public readonly ConcurrentBag<(LogLevel Level, string Message)> Kayitlar = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _owner;
            public CapturingLogger(CapturingLoggerProvider owner) => _owner = owner;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _owner.Kayitlar.Add((logLevel, formatter(state, exception)));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose()
                {
                }
            }
        }
    }

    private sealed record Harness(
        EBelgeOutboxWorker Worker,
        SharedTestState State,
        FakeMetrics Metrics,
        EBelgeOutboxWorkerHealthState HealthState,
        FakeDelay Delay,
        CapturingLoggerProvider Loglar,
        ServiceProvider RootProvider) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            RootProvider.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static EBelgeProcessingOptions HizliTestOptions(bool enabled = true, int maxParallelism = 1, int batchSize = 10) => new()
    {
        Enabled = enabled,
        NotBeforeLocalDate = "2020-01-01",
        TimeZoneId = "Europe/Istanbul",
        PollIntervalSeconds = 1,
        IdlePollIntervalSeconds = 2,
        BatchSize = batchSize,
        LeaseDurationSeconds = 60,
        MaxParallelism = maxParallelism,
        ShutdownGracePeriodSeconds = 3,
    };

    private static Harness CreateHarness(EBelgeProcessingOptions? options = null, TimeProvider? timeProvider = null)
    {
        var state = new SharedTestState();
        // Faz 2B.8 görev md.19-22'nin dolaylı KANITI: transition/retry-policy servisleri BU test
        // container'ına HİÇ KAYDEDİLMEZ - worker bunlara İHTİYAÇ DUYMADIĞI (yalnız claim+işleme
        // servislerini kullandığı) İÇİN, eğer worker YANLIŞLIKLA bunları çözmeye ÇALIŞSAYDI DI
        // "servis kayıtlı değil" hatasıyla PATLARDI. Tüm testlerin BAŞARILI geçmesi, worker'ın
        // İKİNCİ bir complete/fail/retry çağrısı yapmadığının dolaylı KANITIDIR.
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<IEBelgeOutboxClaimLeaseService, FakeClaimLeaseService>();
        services.AddScoped<IEBelgeOutboxMesajIslemeService, FakeMesajIslemeService>();
        var rootProvider = services.BuildServiceProvider();

        var opts = options ?? HizliTestOptions();
        var tp = timeProvider ?? TimeProvider.System;
        var gate = new EBelgeProcessingActivationGate(Options.Create(opts), tp, NullLogger<EBelgeProcessingActivationGate>.Instance);
        var metrics = new FakeMetrics();
        var healthState = new EBelgeOutboxWorkerHealthState(tp);
        var delay = new FakeDelay();
        var loglar = new CapturingLoggerProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(loglar));
        var logger = loggerFactory.CreateLogger<EBelgeOutboxWorker>();

        var worker = new EBelgeOutboxWorker(
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            gate, metrics, healthState, delay, Options.Create(opts), logger);

        return new Harness(worker, state, metrics, healthState, delay, loglar, rootProvider);
    }

    private static EBelgeOutboxClaimLeaseResultDto Claim(int outboxId, EBelgeOutboxIsTuru isTuru = EBelgeOutboxIsTuru.ArtefaktOlustur, int denemeSayisi = 1) => new()
    {
        OutboxMesajiId = outboxId,
        KurumId = 1,
        EBelgeKaydiId = outboxId,
        IsTuru = isTuru,
        Durum = EBelgeOutboxDurumu.Isleniyor,
        DenemeSayisi = denemeSayisi,
        KilitToken = $"GIZLI-TOKEN-{Guid.NewGuid():N}",
        KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(5),
        IslemBaslamaZamaniUtc = DateTime.UtcNow,
    };

    private static async Task WaitUntilAsync(Func<bool> kosul, TimeSpan? zamanAsimi = null)
    {
        var sinir = zamanAsimi ?? TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (!kosul())
        {
            if (sw.Elapsed > sinir)
            {
                throw new TimeoutException("Beklenen koşul zaman aşımı içinde gerçekleşmedi.");
            }

            await Task.Delay(10);
        }
    }

    // ---- Aktivasyon (worker seviyesinde, gate ile birlikte) ----

    [Fact]
    public async Task GateKapaliykenClaimCagrilmazVeIdleGecikmesiKullanilir()
    {
        await using var h = CreateHarness(HizliTestOptions(enabled: false));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, h.State.ClaimCallCount);
        Assert.Equal(TimeSpan.FromSeconds(2), h.Delay.RequestedDelays.First());
    }

    [Fact]
    public async Task GateKapaliykenMesajinDenemeSayisiDegismez()
    {
        // Faz 2B.8 görev md.17 - "gate kapalıyken mesajlar terminal hataya geçirilmez, deneme
        // sayısı artırılmaz, lease alınmaz". Worker claim'e HİÇ GİTMEDİĞİNDEN, deneme sayısını
        // artıran TEK yer (claim servisinin KENDİSİ) hiç ÇAĞRILMAZ - ClaimCallCount == 0 bunun
        // doğrudan kanıtıdır.
        await using var h = CreateHarness(HizliTestOptions(enabled: false));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, h.State.ClaimCallCount);
    }

    // ---- Polling ----

    [Fact]
    public async Task KuyrukBoskenIdleDelayKullanilir()
    {
        await using var h = CreateHarness();
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, h.State.ClaimCallCount);
        Assert.Equal(TimeSpan.FromSeconds(2), h.Delay.RequestedDelays.First());
    }

    [Fact]
    public async Task MesajIslendigindeNormalPollIntervalKullanilir()
    {
        await using var h = CreateHarness();
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(1), h.Delay.RequestedDelays.First());
        Assert.Contains(1, h.State.IslenenOutboxIdler);
    }

    [Fact]
    public async Task PollLoopHerTurdaBeklemeCagirirBusySpinYapmaz()
    {
        // Kuyruk sürekli boş - delay ANINDA döner (BlockUntilCancelled=false) - loop HIZLA
        // ilerleyebilir. N tur SONRA worker'ı durdurup, delay'in HER turda (busy-spin yaparak
        // ATLAMADAN) çağrıldığını doğrular.
        await using var h = CreateHarness();

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 5);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.True(h.Delay.RequestedDelays.Count >= 5);
        Assert.All(h.Delay.RequestedDelays, d => Assert.Equal(TimeSpan.FromSeconds(2), d));
    }

    [Fact]
    public async Task CancellationBekleneniHemenKeser()
    {
        await using var h = CreateHarness();
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);

        var sw = Stopwatch.StartNew();
        await h.Worker.StopAsync(CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"StopAsync {sw.Elapsed} sürdü - cancellation delay'i hemen kesmedi.");
    }

    [Fact]
    public async Task ShutdownSonrasindaYeniClaimYapilmaz()
    {
        await using var h = CreateHarness();
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        var claimSayisiKapatmaSonrasi = h.State.ClaimCallCount;
        await Task.Delay(200);

        Assert.Equal(claimSayisiKapatmaSonrasi, h.State.ClaimCallCount);
    }

    // ---- Scope ve DbContext ----

    [Fact]
    public async Task HerMesajIcinAyriDiScopeOlusturulur()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        h.State.ClaimsToOffer.Enqueue(Claim(3));
        h.Delay.BlockUntilCancelled = false;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 3);
        await h.Worker.StopAsync(CancellationToken.None);

        // Scoped kayıt olduğundan HER scope YENİ bir örnek üretir - 3 mesaj İÇİN 3 FARKLI
        // InstanceId GÖRÜLMÜŞ olmalıdır (bkz. görev md.18 senaryo 13).
        var farkliInstanceSayisi = h.State.IslemeInstanceIdsGorulen.Distinct().Count();
        Assert.Equal(3, farkliInstanceSayisi);
    }

    [Fact]
    public async Task AyniScopedHandlerIkiParalelMesajdaPaylasilmaz()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        var gorulenInstanceIdlerEsZamanli = new ConcurrentBag<Guid>();
        var kapi = new SemaphoreSlim(0, 2);

        h.State.IslemeOverride = async (claim, ct) =>
        {
            // Her ikisi de AYNI ANDA burada bekler - eğer AYNI instance PAYLAŞILSAYDI (Scoped
            // yerine Singleton gibi davransaydı), bu senkronizasyon YİNE de test EDEBİLİR ama asıl
            // kanıt yukarıdaki InstanceId'lerin FARKLI olmasıdır (bkz. aşağıdaki assert).
            kapi.Release();
            await Task.Delay(100, ct);
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 2, TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        var farkliInstanceSayisi = h.State.IslemeInstanceIdsGorulen.Distinct().Count();
        Assert.Equal(2, farkliInstanceSayisi);
    }

    [Fact]
    public async Task BirMesajinExceptionISonrakiMesajinIslenmesiniEngellemez()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        h.State.IslemeOverride = (claim, ct) => claim.OutboxMesajiId == 1
            ? throw new InvalidOperationException("kasıtlı test hatası")
            : Task.FromResult(EBelgeOutboxIslemeSonucu.Tamamlandi());

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 2);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(1, h.State.IslenenOutboxIdler);
        Assert.Contains(2, h.State.IslenenOutboxIdler);
    }

    // ---- Hata dayanıklılığı ----

    [Fact]
    public async Task GeciciSqlClaimHatasiWorkeriDurdurmaz()
    {
        await using var h = CreateHarness();
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("geçici SQL hatası simülasyonu"));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.Delay.BlockUntilCancelled = false;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Contains(1), TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.True(h.Metrics.PollErrorCount >= 1);
        Assert.Contains(1, h.State.IslenenOutboxIdler);
    }

    [Fact]
    public async Task HostCancellationHataVeyaRetryOlarakKaydedilmez()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        var tcs = new TaskCompletionSource();
        h.State.IslemeOverride = async (claim, ct) =>
        {
            tcs.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); // yalnız cancellation ile biter
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await tcs.Task;
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, h.Metrics.PollErrorCount);
        Assert.Empty(h.Metrics.Results);
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task WorkerLevelHataBoundedBackoffUygular()
    {
        await using var h = CreateHarness();
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("worker seviyesi hata"));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        // Hata sonrası kullanılan bekleme, IDLE aralığıdır (kontrollü/bounded backoff) - sıkı bir
        // döngüde HEMEN yeniden denenmez.
        Assert.Equal(TimeSpan.FromSeconds(2), h.Delay.RequestedDelays.First());
    }

    [Fact]
    public async Task FatalExceptionKoruKoruneYutulmaz()
    {
        await using var h = CreateHarness();
        h.State.ClaimExceptions.Enqueue(new OutOfMemoryException());

        await h.Worker.StartAsync(CancellationToken.None);

        // OutOfMemoryException, `ExecuteAsync`'in İÇİNDEKİ genel catch TARAFINDAN
        // yakalanmamalıdır - BackgroundService bunu `ExecuteTask`'a FAULTED olarak YANSITIR.
        var executeTask = GetExecuteTask(h.Worker);
        await Assert.ThrowsAsync<OutOfMemoryException>(() => executeTask!);
    }

    private static Task? GetExecuteTask(EBelgeOutboxWorker worker)
    {
        var field = typeof(BackgroundService).GetField("_executeTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Task?)field?.GetValue(worker);
    }

    // ---- Paralellik ----

    [Fact]
    public async Task MaxParallelismBirIkenTekMesajEsZamanliIslenir()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));
        for (var i = 1; i <= 4; i++)
        {
            h.State.ClaimsToOffer.Enqueue(Claim(i));
        }

        h.State.IslemeOverride = async (claim, ct) =>
        {
            var simdi = Interlocked.Increment(ref h.State.CurrentConcurrent);
            InterlockedMax(ref h.State.MaxConcurrentObserved, simdi);
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref h.State.CurrentConcurrent);
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 4, TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, h.State.MaxConcurrentObserved);
    }

    [Fact]
    public async Task MaxParallelismIkiIkenEnFazlaIkiMesajEsZamanliIslenir()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2));
        for (var i = 1; i <= 6; i++)
        {
            h.State.ClaimsToOffer.Enqueue(Claim(i));
        }

        h.State.IslemeOverride = async (claim, ct) =>
        {
            var simdi = Interlocked.Increment(ref h.State.CurrentConcurrent);
            InterlockedMax(ref h.State.MaxConcurrentObserved, simdi);
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref h.State.CurrentConcurrent);
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 6, TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.True(h.State.MaxConcurrentObserved >= 2, "En az 2 eşzamanlı mesaj GÖZLEMLENMELİYDİ.");
        Assert.True(h.State.MaxConcurrentObserved <= 2, $"MaxParallelism=2 aşıldı: {h.State.MaxConcurrentObserved}");
    }

    [Fact]
    public async Task InflightOlcumuDogruArtarVeAzalir()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimsToOffer.Enqueue(Claim(2));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 2);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, h.Metrics.InflightIncrements);
        Assert.Equal(2, h.Metrics.InflightDecrements);
        Assert.Equal(0, h.HealthState.GetSnapshot().InflightCount);
    }

    private static void InterlockedMax(ref int hedef, int deger)
    {
        int mevcut;
        do
        {
            mevcut = hedef;
            if (deger <= mevcut)
            {
                return;
            }
        } while (Interlocked.CompareExchange(ref hedef, deger, mevcut) != mevcut);
    }

    // ---- Mevcut iş türleri ----

    [Theory]
    [InlineData(EBelgeOutboxIsTuru.ArtefaktOlustur)]
    [InlineData(EBelgeOutboxIsTuru.UblImzala)]
    public async Task DogruIsTuruIleClaimIsleAsyncEGider(EBelgeOutboxIsTuru isTuru)
    {
        await using var h = CreateHarness();
        h.State.ClaimsToOffer.Enqueue(Claim(1, isTuru));
        EBelgeOutboxIsTuru? gorulenIsTuru = null;
        h.State.IslemeOverride = (claim, ct) =>
        {
            gorulenIsTuru = claim.IsTuru;
            return Task.FromResult(EBelgeOutboxIslemeSonucu.Tamamlandi());
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => gorulenIsTuru is not null);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(isTuru, gorulenIsTuru);
    }

    [Fact]
    public async Task RetryPlanlandiSonucuGozlemlenirIkinciBirIslemYapilmaz()
    {
        await using var h = CreateHarness();
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ResultsByOutboxId[1] = EBelgeOutboxIslemeSonucu.RetryPlanlandi(TimeSpan.FromMinutes(5));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Metrics.Results.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        var sonuc = Assert.Single(h.Metrics.Results);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, sonuc.SonucTuru);
    }

    // ---- Gözlemlenebilirlik ----

    [Theory]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.Tamamlandi)]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.TerminalHata)]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi)]
    public async Task HerSonucTuruDogruMetrigiArtirir(EBelgeOutboxIslemeSonucuTuru sonucTuru)
    {
        await using var h = CreateHarness();
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ResultsByOutboxId[1] = sonucTuru switch
        {
            EBelgeOutboxIslemeSonucuTuru.Tamamlandi => EBelgeOutboxIslemeSonucu.Tamamlandi(),
            EBelgeOutboxIslemeSonucuTuru.TerminalHata => EBelgeOutboxIslemeSonucu.TerminalHata(),
            EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi => EBelgeOutboxIslemeSonucu.SahiplikKaybedildi(),
            _ => throw new ArgumentOutOfRangeException(nameof(sonucTuru)),
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Metrics.Results.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        var sonuc = Assert.Single(h.Metrics.Results);
        Assert.Equal(sonucTuru, sonuc.SonucTuru);
    }

    [Fact]
    public async Task LoglardaLeaseTokenBulunmaz()
    {
        await using var h = CreateHarness();
        var claim = Claim(1);
        h.State.ClaimsToOffer.Enqueue(claim);

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains(claim.KilitToken, StringComparison.Ordinal));
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains("GIZLI-TOKEN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BosKuyrukPollingLogSpamUretmez()
    {
        await using var h = CreateHarness();

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 5);
        await h.Worker.StopAsync(CancellationToken.None);

        // Boş kuyrukta "mesaj işlendi" seviyesinde bir bilgi/uyarı/hata logu ÜRETİLMEMELİDİR -
        // yalnız worker başlangıç/bitiş logları (2 adet) olabilir.
        var bilgiVeUstuLoglar = h.Loglar.Kayitlar.Where(k => k.Level >= LogLevel.Information).ToList();
        Assert.True(bilgiVeUstuLoglar.Count <= 2, $"Boş kuyrukta beklenenden fazla log üretildi: {bilgiVeUstuLoglar.Count}");
    }

    [Fact]
    public async Task BasariliMesajInformationSeviyesindeLoglanir()
    {
        await using var h = CreateHarness();
        h.State.ClaimsToOffer.Enqueue(Claim(42));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(h.Loglar.Kayitlar, k => k.Level == LogLevel.Information && k.Message.Contains("42", StringComparison.Ordinal));
    }
}
