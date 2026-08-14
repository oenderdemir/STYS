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
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
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

        /// <summary>Faz 2B.8.2 görev md.6/md.12 testleri İÇİN - `ProcessClaimAsync`'in `try` BLOĞU içinden GERÇEKTEN worker-altyapısı KAYNAKLI bir exception (ör. `OutOfMemoryException`) FIRLATMASINI simüle eder.</summary>
        public Action? IncrementInflightOverride;

        /// <summary>Faz 2B.8.2 görev md.6/md.12 testleri İÇİN - `ProcessClaimAsync`'in `finally` BLOĞU içinden bir exception FIRLATMASINI simüle eder (bu, `Task.WhenAll`'ın KENDİSİNİN hata ÜRETMESİNİN TEK gerçekçi yoludur - `try/catch` zaten TÜM mesaj-seviyesi hataları YAKALAR).</summary>
        public Action? DecrementInflightOverride;

        public void RecordClaimed(EBelgeOutboxIsTuru isTuru) => Interlocked.Increment(ref ClaimedCount);
        public void RecordResult(EBelgeOutboxIsTuru isTuru, EBelgeOutboxIslemeSonucuTuru sonucTuru, TimeSpan sure) => Results.Add((isTuru, sonucTuru, sure));
        public void RecordPollError() => Interlocked.Increment(ref PollErrorCount);

        public void IncrementInflight()
        {
            Interlocked.Increment(ref InflightIncrements);
            IncrementInflightOverride?.Invoke();
        }

        public void DecrementInflight()
        {
            Interlocked.Increment(ref InflightDecrements);
            DecrementInflightOverride?.Invoke();
        }
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

    /// <summary>
    /// Faz 2B.8.1 görev md.6 - "Mevcut test logger'ı exception nesnesini çıktıya eklemediği için
    /// production davranışını simüle ETMİYOR". Bu sürüm, `formatter(state, exception) +
    /// exception?.ToString()` şeklinde - GERÇEK sağlayıcıların (Serilog console/file sink'leri gibi
    /// - bu çözümde ZATEN kullanılan) davranışına YAKIN - çıktı üretir: `ILogger.LogError(ex,
    /// "şablon")` çağrıldığında, exception NESNESİ logger'a GEÇİRİLMİŞSE, bu sınıf onun
    /// `ToString()`'ini (mesaj + stack trace + inner exception'lar DAHİL) render EDİLMİŞ çıktıya
    /// EKLER - tıpkı gerçek bir sağlayıcının yapacağı gibi.
    /// </summary>
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
                var renderedMesaj = formatter(state, exception);
                if (exception is not null)
                {
                    renderedMesaj = renderedMesaj + Environment.NewLine + exception;
                }

                _owner.Kayitlar.Add((logLevel, renderedMesaj));
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

    // ---- Faz 2B.8.1: semaphore ve task yaşam döngüsü ----

    [Fact]
    public async Task IkinciClaimExceptionUretirseIlkTaskMutlakaAwaitEdilir()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        // Faz 2B.8.1 - FakeClaimLeaseService, kuyruktan `null` bir "exception" DEQUEUE ettiğinde
        // (ex is not null KOŞULU false olduğundan) FIRLATMAZ - bu, İLK claim çağrısını (mesaj 1
        // İÇİN) etkilemeyen bir "yer tutucu" olarak KULLANILIR; İKİNCİ çağrı GERÇEK exception'ı alır.
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("ikinci claim kasıtlı hata"));
        var ilkTaskTamamlandi = new TaskCompletionSource();
        h.State.IslemeOverride = async (claim, ct) =>
        {
            await Task.Delay(150, ct);
            ilkTaskTamamlandi.TrySetResult();
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await ilkTaskTamamlandi.Task;
        await h.Worker.StopAsync(CancellationToken.None);

        // İlk task GERÇEKTEN tamamlandı (await edildiği KANITI) - metriğe sonucu YAZDI.
        Assert.Contains(1, h.State.IslenenOutboxIdler);
        Assert.Single(h.Metrics.Results, r => r.SonucTuru == EBelgeOutboxIslemeSonucuTuru.Tamamlandi);
        // İkinci (başarısız) claim, bir poll hatası olarak KAYDEDİLDİ - tur exception'ı YUTULMADI.
        Assert.True(h.Metrics.PollErrorCount >= 1);
    }

    [Fact]
    public async Task DisposeEdilmisSemaphoreUzerindeReleaseCagrilmazExceptionOlusmaz()
    {
        // Faz 2B.8.1 görev md.12 test 2 - önceki testle AYNI yarış senaryosu; burada özellikle
        // `ObjectDisposedException` (semaphore ERKEN dispose edilseydi Release() bunu fırlatırdı)
        // hiçbir logda GÖRÜNMEDİĞİNİ ve worker'ın normal şekilde DURDUĞUNU doğrular.
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("ikinci claim kasıtlı hata"));
        var ilkTaskTamamlandi = new TaskCompletionSource();
        h.State.IslemeOverride = async (claim, ct) =>
        {
            await Task.Delay(150, ct);
            ilkTaskTamamlandi.TrySetResult();
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await ilkTaskTamamlandi.Task;
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains(nameof(ObjectDisposedException), StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnobservedTaskExceptionOlusmaz()
    {
        var unobserved = new List<Exception>();
        void Handler(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            unobserved.Add(args.Exception);
            args.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            await using (var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5)))
            {
                h.State.ClaimsToOffer.Enqueue(Claim(1));
                h.State.ClaimExceptions.Enqueue(null);
                h.State.ClaimExceptions.Enqueue(new InvalidOperationException("ikinci claim kasıtlı hata"));

                await h.Worker.StartAsync(CancellationToken.None);
                await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 1);
                await h.Worker.StopAsync(CancellationToken.None);
            }

            // Faz 2B.8.1 görev md.2 - tüm dispatch edilmiş task'lar `Task.WhenAll` İLE await
            // edildiğinden, GC/finalization sırasında unobserved exception OLUŞMAMALIDIR.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }

        Assert.Empty(unobserved);
    }

    [Fact]
    public async Task OncekiTurunTasklariTamamlanmadanSonrakiTurBaslamaz()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 2));
        for (var i = 1; i <= 4; i++)
        {
            h.State.ClaimsToOffer.Enqueue(Claim(i));
        }

        var olaylar = new ConcurrentQueue<(string Olay, int OutboxId, long Zaman)>();
        var sw = Stopwatch.StartNew();
        h.State.IslemeOverride = async (claim, ct) =>
        {
            await Task.Delay(80, ct);
            olaylar.Enqueue(("TaskBitti", claim.OutboxMesajiId, sw.ElapsedTicks));
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => olaylar.Count >= 4, TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        // BatchSize=2 - ilk tur (1,2) mesajlarını, ikinci tur (3,4) mesajlarını claim eder. İlk
        // turun İKİ task'ının da bitiş zamanı, İKİNCİ turun claim ettiği mesajların İŞLENME
        // (task bitiş) zamanlarından ÖNCE olmalıdır (turlar SIRALI, iç içe GEÇMEZ).
        var listeliOlaylar = olaylar.OrderBy(o => o.Zaman).ToList();
        Assert.Equal(4, listeliOlaylar.Count);
        var ilkIkiOutboxId = listeliOlaylar.Take(2).Select(o => o.OutboxId).ToHashSet();
        var sonIkiOutboxId = listeliOlaylar.Skip(2).Select(o => o.OutboxId).ToHashSet();
        Assert.Equal(new HashSet<int> { 1, 2 }, ilkIkiOutboxId);
        Assert.Equal(new HashSet<int> { 3, 4 }, sonIkiOutboxId);
    }

    [Fact]
    public async Task PollingTurlariArasindaToplamEsZamanliMesajSayisiMaxParallelismiAsmaz()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 2));
        for (var i = 1; i <= 8; i++)
        {
            h.State.ClaimsToOffer.Enqueue(Claim(i));
        }

        h.State.IslemeOverride = async (claim, ct) =>
        {
            var simdi = Interlocked.Increment(ref h.State.CurrentConcurrent);
            InterlockedMax(ref h.State.MaxConcurrentObserved, simdi);
            await Task.Delay(25, ct);
            Interlocked.Decrement(ref h.State.CurrentConcurrent);
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 8, TimeSpan.FromSeconds(15));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.True(h.State.MaxConcurrentObserved <= 2, $"MaxParallelism=2, TÜM polling turları BOYUNCA aşılmamalıydı: {h.State.MaxConcurrentObserved}");
    }

    [Fact]
    public async Task ClaimNullDondugundePermitGeriBirakilirVeSonrakiTurCalisir()
    {
        // BlockUntilCancelled=false (varsayılan) BIRAKILIR - boş kuyrukla BİRDEN FAZLA turun HIZLA
        // geçmesine İZİN VERİR (delay ANINDA döner). Permit sızıntısı OLSAYDI, İLERLEYEN turlarda
        // `semaphore.WaitAsync` SONSUZA dek bloke OLUR, worker asla YENİ eklenen mesajı claim
        // EDEMEZDİ.
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.ClaimCallCount >= 3); // en az birkaç boş tur GEÇSİN

        h.State.ClaimsToOffer.Enqueue(Claim(99));

        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Contains(99), TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(99, h.State.IslenenOutboxIdler);
    }

    [Fact]
    public async Task ClaimCancellationOlusturuncaPermitGeriBirakilirVeWorkerDevamEder()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 1));
        // Gerçek stoppingToken'a BAĞLI OLMAYAN, doğrudan fırlatılan bir OperationCanceledException -
        // ExecuteAsync'in "host cancellation" filtresine UYMADIĞINDAN normal bir worker-seviyesi
        // hata olarak İŞLENİR (bkz. görev md.12 test 8).
        h.State.ClaimExceptions.Enqueue(new OperationCanceledException("sahte/gerçek olmayan iptal"));
        h.State.ClaimsToOffer.Enqueue(Claim(7));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Contains(7), TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(7, h.State.IslenenOutboxIdler);
        Assert.True(h.Metrics.PollErrorCount >= 1);
    }

    [Fact]
    public async Task ScopeVeyaDiCozumlemeHatasindaPermitGeriBirakilirVeWorkerDevamEder()
    {
        // Faz 2B.8.1 görev md.4/md.12 test 9 - `IEBelgeOutboxClaimLeaseService` KASITLI OLARAK
        // KAYITLI DEĞİL - `GetRequiredService` her claim denemesinde `InvalidOperationException`
        // fırlatır (gerçek bir DI çözümleme hatasını TEMSİL eder). Worker, bu hatayı TEKRAR TEKRAR
        // (birden fazla tur boyunca) GÜVENLE atlatabilmelidir - bu, permit'in HİÇBİR turda
        // SIZDIRILMADIĞININ dolaylı kanıtıdır (sızıntı olsaydı semaphore tükenir, İLERLEYEN
        // turlarda `WaitAsync` SONSUZA dek bloke OLURDU).
        var services = new ServiceCollection();
        services.AddScoped<IEBelgeOutboxMesajIslemeService, FakeMesajIslemeService>();
        services.AddSingleton(new SharedTestState());
        var rootProvider = services.BuildServiceProvider();

        var opts = HizliTestOptions();
        var gate = new EBelgeProcessingActivationGate(Options.Create(opts), TimeProvider.System, NullLogger<EBelgeProcessingActivationGate>.Instance);
        var metrics = new FakeMetrics();
        var healthState = new EBelgeOutboxWorkerHealthState(TimeProvider.System);
        var delay = new FakeDelay(); // BlockUntilCancelled=false - birden fazla turun HIZLA geçmesine İZİN VERİR.
        var worker = new EBelgeOutboxWorker(
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            gate, metrics, healthState, delay, Options.Create(opts), NullLogger<EBelgeOutboxWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => delay.RequestedDelays.Count >= 3, TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(metrics.PollErrorCount >= 3, $"Worker, TEKRARLANAN DI çözümleme hatalarından SONRA bile ilerlemeye devam edebilmeliydi (gözlemlenen hata sayısı: {metrics.PollErrorCount}).");

        await rootProvider.DisposeAsync();
    }

    [Fact]
    public async Task UcuncuClaimHataVersinIlkIkiTaskYineDeGozlemlenirVeAwaitEdilir()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 3, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("üçüncü claim kasıtlı hata"));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 2, TimeSpan.FromSeconds(10));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(1, h.State.IslenenOutboxIdler);
        Assert.Contains(2, h.State.IslenenOutboxIdler);
        Assert.Equal(2, h.Metrics.Results.Count(r => r.SonucTuru == EBelgeOutboxIslemeSonucuTuru.Tamamlandi));
    }

    [Fact]
    public async Task StopSirasindaClaimExceptionIleProcessingTaskYarisiDeadlockOlusturmaz()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("ikinci claim kasıtlı hata"));
        var islemeBasladi = new TaskCompletionSource();
        h.State.IslemeOverride = async (claim, ct) =>
        {
            islemeBasladi.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); // yalnız cancellation İLE biter
            return EBelgeOutboxIslemeSonucu.Tamamlandi();
        };

        await h.Worker.StartAsync(CancellationToken.None);
        await islemeBasladi.Task;

        var sw = Stopwatch.StartNew();
        await h.Worker.StopAsync(CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"StopAsync {sw.Elapsed} sürdü - claim hatası/processing task yarışı DEADLOCK oluşturmuş OLABİLİR.");
    }

    [Fact]
    public async Task TurTamamlandigindaInflightVeSemaphorePermitBaslangicaDoner()
    {
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("ikinci claim kasıtlı hata"));

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Count >= 1, TimeSpan.FromSeconds(30));
        // Bir SONRAKİ (temiz) turun da BAŞARIYLA claim/işleme YAPABİLDİĞİNİ doğrulayarak - semaphore
        // permit'lerinin başlangıç değerine DÖNDÜĞÜNÜ (aksi halde İLERLEYEN turlar TIKANIRDI) dolaylı
        // olarak KANITLAR.
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Contains(2), TimeSpan.FromSeconds(30));
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, h.HealthState.GetSnapshot().InflightCount);
    }

    // ---- Faz 2B.8.1/2B.9: güvenli loglama ----
    // Üç ayrı [Fact] (lease token / XML+VKN / password+SignatureValue) AYNI kod yolunu (claim
    // exception -> worker-seviyesi güvenli loglama) ve AYNI dependency seviyesini (in-memory fake
    // harness) çalıştırıyordu; yalnız exception mesajındaki gizli değer(ler) DEĞİŞİYORDU. Tek bir
    // [Theory]'e birleştirildi - hiçbir senaryo/assertion KAYBOLMADI (üçüncü senaryonun İKİ gizli
    // değeri AYNI mesajda BİRLİKTE test edilmesi de KORUNDU).

    public static IEnumerable<object[]> LoglanmamasiGerekenGizliDegerSenaryolari() => new[]
    {
        new object[] { "Bağlantı hatası - KilitToken=GIZLI-TOKEN-123", new[] { "GIZLI-TOKEN-123" } },
        new object[] { "Doğrulama hatası: <VKN>1234567890</VKN>", new[] { "<VKN>1234567890</VKN>", "1234567890" } },
        new object[] { "Bağlantı: Password=secret; SignatureValue=secret", new[] { "Password=secret", "SignatureValue=secret", "secret" } },
    };

    [Theory]
    [MemberData(nameof(LoglanmamasiGerekenGizliDegerSenaryolari))]
    public async Task WorkerLevelExceptionMesajindakiGizliDegerLogaSizmaz(string exceptionMesaji, string[] gizliDegerler)
    {
        await using var h = CreateHarness();
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException(exceptionMesaji));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1, TimeSpan.FromSeconds(15));
        await h.Worker.StopAsync(CancellationToken.None);

        foreach (var gizliDeger in gizliDegerler)
        {
            Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains(gizliDeger, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task HamExceptionToStringProductionLoggeraVerilmez()
    {
        await using var h = CreateHarness();
        const string gizliMesaj = "GIZLI-TOKEN-123 <VKN>1234567890</VKN> Password=secret SignatureValue=secret";
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException(gizliMesaj));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1, TimeSpan.FromSeconds(15));
        await h.Worker.StopAsync(CancellationToken.None);

        // Test logger'ı GERÇEKÇİDİR (formatter + exception?.ToString()) - bu yüzden ex NESNESİ
        // logger'a GEÇİRİLSEYDİ, exception'ın MESAJI (gizli değerler DAHİL) VE bir stack trace
        // işareti ("at ") BURADA GÖRÜNÜRDÜ. Worker'ın KENDİSİ `ex`'i logger'a hiç GEÇİRMEDİĞİ İÇİN
        // (bkz. LogWorkerLevelHataGuvenli - yalnız `ex.GetType().Name` GÜVENLİ bir alan olarak
        // loglanır, TAM exception/mesajı/ToString() DEĞİL) - bu ASLA olmaz. `ExceptionType=
        // InvalidOperationException` GÜVENLİ/BEKLENEN bir alan olduğundan (yalnız TİP ADI, mesaj
        // DEĞİL) BURADA reddedilmez - yalnız GİZLİ MESAJ İÇERİĞİ VE stack trace işareti kontrol
        // edilir.
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains(gizliMesaj, StringComparison.Ordinal));
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains("GIZLI-TOKEN-123", StringComparison.Ordinal));
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains("   at ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GuvenliHataKoduVeExceptionTypeLoglanir()
    {
        await using var h = CreateHarness();
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("herhangi bir iç hata"));
        h.Delay.BlockUntilCancelled = true;

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Delay.RequestedDelays.Count >= 1);
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Contains(h.Loglar.Kayitlar, k => k.Level == LogLevel.Error
            && k.Message.Contains("EBELGE_OUTBOX_WORKER_BEKLENMEYEN_HATA", StringComparison.Ordinal)
            && k.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
    }

    [Fact]
    public void AktivasyonConfigHatasiHerTurdaLogSpamUretmez()
    {
        var loglar = new CapturingLoggerProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(loglar));
        var logger = loggerFactory.CreateLogger<EBelgeProcessingActivationGate>();
        var options = new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "gecersiz-tarih", TimeZoneId = "Europe/Istanbul" };
        var gate = new EBelgeProcessingActivationGate(Options.Create(options), TimeProvider.System, logger);

        for (var i = 0; i < 10; i++)
        {
            gate.Evaluate();
        }

        var hataLoglari = loglar.Kayitlar.Count(k => k.Level == LogLevel.Error);
        Assert.Equal(1, hataLoglari);
    }

    // ---- Faz 2B.8.2 görev md.6/md.7: semaphore kesin disposal + claim/task hatası önceliği ----

    [Fact]
    public async Task TaskWhenAllExceptionUretseBileSemaphoreDisposeEdilirVeFatalExceptionYayilir()
    {
        var unobserved = new List<Exception>();
        void Handler(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            unobserved.Add(args.Exception);
            args.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            await using (var h = CreateHarness(HizliTestOptions(maxParallelism: 1)))
            {
                h.State.ClaimsToOffer.Enqueue(Claim(1));
                // Faz 2B.8.2 görev md.6 - `ProcessClaimAsync`'in `try` bloğu İÇİNDEN, worker
                // ALTYAPISI kaynaklı (mesaj işleme İLE İLGİSİZ) GERÇEK bir `OutOfMemoryException` -
                // `catch (Exception ex) when (ex is not OutOfMemoryException)` filtresi BUNU
                // YAKALAMAZ, dispatch edilen TASK GERÇEKTEN faulted olur - `Task.WhenAll`
                // `BirTurCalistirAsync`'in `finally` bloğunda BUNU fırlatır.
                h.Metrics.IncrementInflightOverride = () => throw new OutOfMemoryException();

                await h.Worker.StartAsync(CancellationToken.None);

                var executeTask = GetExecuteTask(h.Worker);
                // Faz 2B.8.2 görev md.6 kural 1 - fatal exception HİÇBİR sarmalayıcı OLMADAN,
                // orijinal TİPİYLE yayılır (genel bir catch İLE GİZLENMEZ).
                await Assert.ThrowsAsync<OutOfMemoryException>(() => executeTask!);
            }

            // Faz 2B.8.2 görev md.6/md.12 test 13 - `Task.WhenAll` İLE await edildiğinden, GC/
            // finalization sırasında unobserved exception OLUŞMAMALIDIR (semaphore'un KENDİSİ İSE
            // İÇ finally'de dispose EDİLMİŞTİR - C#'ın try/finally dil GARANTİSİ - bkz. görev
            // md.6 kural, "semaphore.Dispose() İÇ finally'de, Task.WhenAll SONUCU NE OLURSA
            // OLSUN çalışır").
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }

        Assert.Empty(unobserved);
    }

    [Fact]
    public async Task ClaimHatasiVeTaskAltyapiHatasiAyniTurdaOlusursaWorkerCokmezVeDevamEder()
    {
        // Faz 2B.8.2 görev md.6/md.7 test 14 - AYNI turda HEM bir claim hatası (İKİNCİ claim
        // denemesi) HEM bir task-altyapısı hatası (`ProcessClaimAsync`'in `finally`'inden -
        // `Task.WhenAll`'ın GERÇEKTEN hata ÜRETMESİNİN, mesaj-seviyesi try/catch'i BYPASS eden TEK
        // gerçekçi yolu) OLUŞUR - HİÇBİRİ diğerini SESSİZCE EZMEZ (bkz.
        // `RethrowTurVeTaskHatalariGuvenliSekilde`, "İKİSİ de VARSA VE İKİSİ de fatal/cancellation
        // DEĞİLSE GÜVENLİ bir AggregateException İÇİNDE BİRLEŞTİRİLİR"). Worker BUNU bir POLL
        // hatası olarak İŞLER (ExecuteTask FAULT ETMEZ) - bir SONRAKİ turda BAŞKA bir mesajı
        // BAŞARIYLA claim/işleme YAPABİLDİĞİ GÖSTERİLEREK worker'ın ÇÖKMEDİĞİ KANITLANIR.
        await using var h = CreateHarness(HizliTestOptions(maxParallelism: 2, batchSize: 5));
        h.State.ClaimsToOffer.Enqueue(Claim(1));
        h.State.ClaimExceptions.Enqueue(null);
        h.State.ClaimExceptions.Enqueue(new InvalidOperationException("claim hatasi - ikinci deneme"));
        h.Metrics.DecrementInflightOverride = () => throw new InvalidOperationException("task altyapisi hatasi");

        await h.Worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => h.Metrics.PollErrorCount >= 1, TimeSpan.FromSeconds(10));

        // Hata KAYNAĞI kaldırılır - worker'ın GERÇEKTEN İLERİDE de çalışabildiğini (çökmediğini)
        // KANITLAMAK İçin.
        h.Metrics.DecrementInflightOverride = null;
        h.State.ClaimsToOffer.Enqueue(Claim(2));
        await WaitUntilAsync(() => h.State.IslenenOutboxIdler.Contains(2), TimeSpan.FromSeconds(10));

        await h.Worker.StopAsync(CancellationToken.None);

        // Faz 2B.8.2 görev md.6 kural "ham exception loglanmamalı" - İKİ hatanın da METNİ HİÇBİR
        // log kaydında GEÇMEZ (AggregateException'a SARILMIŞ olsalar BİLE, `LogWorkerLevelHataGuvenli`
        // yalnız güvenli hata kodu/exception TİP adını loglar).
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains("claim hatasi", StringComparison.Ordinal));
        Assert.DoesNotContain(h.Loglar.Kayitlar, k => k.Message.Contains("task altyapisi hatasi", StringComparison.Ordinal));
    }
}
