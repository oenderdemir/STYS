using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8/2B.8.1/2B.8.2 görev md.9/md.12 - worker health check'in aktivasyon-reason-tabanlı
/// Healthy/Degraded/Unhealthy politikasını doğrular. PII/token/ham config değeri İÇERMEZ.
///
/// Faz 2B.8.2 - "Fresh" (worker loop'un HİÇ çalışmadığı) senaryolar İÇİN `RecordActivationDecision`
/// KASITLI OLARAK ELLE ÇAĞRILMAZ (bkz. görev md.9, "production açığını gizlemek için activation
/// kararını elle seed etme") - GERÇEK bir `EBelgeProcessingActivationGate` + GERÇEK `EBelgeProcessingOptions`
/// kullanılır, health check'in KENDİSİNİN doğru fallback DEĞERLENDİRMESİ yaptığı DOĞRUDAN
/// kanıtlanır. Yalnız "worker ZATEN değerlendirme YAPTI" senaryolarını (recovery/staleness/
/// gereksiz-tekrar-değerlendirme-yok) test eden testler `RecordActivationDecision`'ı AÇIKÇA
/// çağırır - bu, worker'ın KENDİ davranışını SİMÜLE eder, production açığını GİZLEMEZ.
/// </summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
public class EBelgeOutboxWorkerHealthCheckTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _zaman;
        public MutableTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public void SetUtcNow(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    /// <summary>Bkz. görev md.8 - `Evaluate()`'in KAÇ KEZ çağrıldığını sayar (health check'in gereksiz ikinci değerlendirme YAPMADIĞINI kanıtlamak için).</summary>
    private sealed class CountingActivationGate : IEBelgeProcessingActivationGate
    {
        private readonly IEBelgeProcessingActivationGate _inner;
        public int EvaluateCallCount;
        public CountingActivationGate(IEBelgeProcessingActivationGate inner) => _inner = inner;

        public EBelgeProcessingActivationDecision Evaluate()
        {
            Interlocked.Increment(ref EvaluateCallCount);
            return _inner.Evaluate();
        }

        public bool ShouldProcess() => Evaluate().CanProcess;
    }

    private sealed class ErrorCountingLoggerProvider : ILoggerProvider
    {
        public int ErrorCount;
        public ILogger CreateLogger(string categoryName) => new CountingLogger(this);
        public void Dispose()
        {
        }

        private sealed class CountingLogger : ILogger
        {
            private readonly ErrorCountingLoggerProvider _owner;
            public CountingLogger(ErrorCountingLoggerProvider owner) => _owner = owner;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error)
                {
                    Interlocked.Increment(ref _owner.ErrorCount);
                }
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

    private static readonly DateTimeOffset Baslangic = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static IEBelgeProcessingActivationGate CreateGate(EBelgeProcessingOptions options, TimeProvider timeProvider, ILogger<EBelgeProcessingActivationGate>? logger = null)
        => new EBelgeProcessingActivationGate(Options.Create(options), timeProvider, logger ?? NullLogger<EBelgeProcessingActivationGate>.Instance);

    private static EBelgeOutboxWorkerHealthCheck CreateCheck(
        IEBelgeOutboxWorkerHealthState healthState, IEBelgeProcessingActivationGate gate, EBelgeProcessingOptions options, DateTimeOffset now)
        => new(healthState, gate, Options.Create(options), new FixedTimeProvider(now));

    private static EBelgeOutboxWorkerHealthCheck CreateCheck(
        IEBelgeOutboxWorkerHealthState healthState, IEBelgeProcessingActivationGate gate, EBelgeProcessingOptions options, TimeProvider timeProvider)
        => new(healthState, gate, Options.Create(options), timeProvider);

    private static EBelgeProcessingOptions Options_(bool enabled = true, string? notBeforeLocalDate = "2020-01-01", string timeZoneId = "Europe/Istanbul") => new()
    {
        Enabled = enabled,
        NotBeforeLocalDate = notBeforeLocalDate,
        TimeZoneId = timeZoneId,
        PollIntervalSeconds = 10,
        IdlePollIntervalSeconds = 30,
    };

    // ---- Faz 2B.9 - "fresh" state, ELLE seed YOK, GERÇEK gate - aktivasyon health karar matrisi ----
    // Bu 5 senaryo öncesinde ayrı [Fact]'lerdi; hepsi AYNI kod yolunu (health check'in kendi fallback
    // Evaluate() değerlendirmesini), AYNI dependency seviyesini (in-memory, gerçek gate + gerçek
    // options) çalıştırıyor - yalnız options/beklenen sonuç DEĞİŞİYORDU. Hata teşhisi ZAYIFLAMADI:
    // xUnit her satırı kendi parametreleriyle ayrı ayrı raporlar.

    public static IEnumerable<object[]> AktivasyonHealthMatrisSenaryolari() => new[]
    {
        new object[] { true, "2020-01-01", "Europe/Istanbul", HealthStatus.Unhealthy, EBelgeProcessingActivationReason.Active },
        new object[] { false, "2020-01-01", "Europe/Istanbul", HealthStatus.Healthy, EBelgeProcessingActivationReason.Disabled },
        new object[] { true, "2030-01-01", "Europe/Istanbul", HealthStatus.Healthy, EBelgeProcessingActivationReason.BeforeActivationDate },
        new object[] { true, "gecersiz-tarih", "Europe/Istanbul", HealthStatus.Degraded, EBelgeProcessingActivationReason.InvalidDateConfiguration },
        new object[] { true, "2020-01-01", "Gecersiz/TimeZone-Kimligi", HealthStatus.Degraded, EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration },
    };

    [Theory]
    [MemberData(nameof(AktivasyonHealthMatrisSenaryolari))]
    public async Task FreshStateAktivasyonKarariBeklenenHealthSonucunuUretir(
        bool enabled, string notBeforeLocalDate, string timeZoneId, HealthStatus beklenenDurum, EBelgeProcessingActivationReason beklenenNeden)
    {
        // Worker loop HİÇ ÇALIŞMADI (`RecordActivationDecision`/`RecordLoopStarted` HİÇBİRİ
        // çağrılmadı) - health check YİNE DE GERÇEK activation durumunu KENDİSİ değerlendirip
        // GÖREBİLMELİDİR (üretim açığını gizleyen elle seed YOK).
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: enabled, notBeforeLocalDate: notBeforeLocalDate, timeZoneId: timeZoneId);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);

        var check = CreateCheck(healthState, gate, options, timeProvider);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(beklenenDurum, sonuc.Status);
        Assert.Equal(beklenenNeden.ToString(), sonuc.Data["activationReason"]);
    }

    // ---- Faz 2B.8.2 görev md.7 test senaryo 7-8: fallback yazımı + gereksiz ikinci değerlendirme YOK ----

    [Fact]
    public async Task HealthFallbackDegerlendirmesiStateYeTypeSafeKararYazar()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: false);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);

        Assert.False(healthState.GetSnapshot().ActivationEvaluated);

        var check = CreateCheck(healthState, gate, options, timeProvider);
        await check.CheckHealthAsync(new HealthCheckContext());

        var snapshotSonra = healthState.GetSnapshot();
        Assert.True(snapshotSonra.ActivationEvaluated);
        Assert.Equal(EBelgeProcessingActivationReason.Disabled, snapshotSonra.ActivationReason);
    }

    [Fact]
    public async Task WorkerDegerlendirmeYaptiktanSonraHealthGereksizIkinciDegerlendirmeYapmaz()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true, notBeforeLocalDate: "2020-01-01");
        var countingGate = new CountingActivationGate(CreateGate(options, timeProvider));
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);

        // Worker'ın KENDİ turunun YAPACAĞI AKIŞ - `EBelgeOutboxWorker.BirTurCalistirAsync` İLE AYNI.
        var workerKarari = countingGate.Evaluate();
        healthState.RecordActivationDecision(workerKarari);
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();
        Assert.Equal(1, countingGate.EvaluateCallCount);

        var check = CreateCheck(healthState, countingGate, options, timeProvider);
        await check.CheckHealthAsync(new HealthCheckContext());
        await check.CheckHealthAsync(new HealthCheckContext());

        // Health check, LoopStarted=true VE ActivationEvaluated=true GÖRDÜĞÜNDEN worker'ın SON
        // kararını GÜVENİR - `Evaluate()` BİR KEZ BİLE TEKRAR ÇAĞRILMAZ.
        Assert.Equal(1, countingGate.EvaluateCallCount);
    }

    // ---- Faz 2B.8.2 görev md.7 test senaryo 9: tarih sınırı, worker başlamadan health tarafından fark edilir ----

    [Fact]
    public async Task WorkerBaslamadanTarihSinirGecerseHealthBeforeActivationDatedenActiveUnhealthyGecer()
    {
        // Europe/Istanbul UTC+3 - "2026-09-15" yerel gün başlangıcı = 2026-09-14T21:00:00Z.
        var oncesi = new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.Zero);
        var sonrasi = new DateTimeOffset(2026, 9, 14, 22, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(oncesi);
        var options = Options_(enabled: true, notBeforeLocalDate: "2026-09-15");
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        var check = CreateCheck(healthState, gate, options, timeProvider);

        var ilkSonuc = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, ilkSonuc.Status);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.BeforeActivationDate), ilkSonuc.Data["activationReason"]);

        // Zaman sınırı GEÇER - worker (RecordLoopStarted HİÇ çağrılmadı) HÂLÂ başlamamıştır.
        timeProvider.SetUtcNow(sonrasi);

        var ikinciSonuc = await check.CheckHealthAsync(new HealthCheckContext());

        // Faz 2B.8.2 görev md.5 - eski `BeforeActivationDate` kararı SONSUZA dek CACHE'LENMEZ;
        // loop HÂLÂ başlamadığından health, HER çağrıda TAZE değerlendirir.
        Assert.Equal(nameof(EBelgeProcessingActivationReason.Active), ikinciSonuc.Data["activationReason"]);
        Assert.Equal(HealthStatus.Unhealthy, ikinciSonuc.Status);
    }

    // ---- Faz 2B.8.2 görev md.7 test senaryo 10-11: log spam yok, ham config değeri yok ----

    [Fact]
    public async Task HealthEvaluationGecersizConfigLogSpamUretmez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true, notBeforeLocalDate: "gecersiz-tarih");
        var loglar = new ErrorCountingLoggerProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(loglar));
        var gate = CreateGate(options, timeProvider, loggerFactory.CreateLogger<EBelgeProcessingActivationGate>());
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        var check = CreateCheck(healthState, gate, options, timeProvider);

        for (var i = 0; i < 5; i++)
        {
            await check.CheckHealthAsync(new HealthCheckContext());
        }

        Assert.Equal(1, loglar.ErrorCount);
    }

    [Fact]
    public async Task FreshStateHealthOutputHamTarihVeyaTimezoneDegeriIcermez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true, notBeforeLocalDate: "gecersiz-tarih", timeZoneId: "Europe/Istanbul");
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        var check = CreateCheck(healthState, gate, options, timeProvider);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        var serileştirilmis = string.Join(" ", sonuc.Data.Select(kv => $"{kv.Key}={kv.Value}"));
        Assert.DoesNotContain("gecersiz-tarih", serileştirilmis, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Mevcut (Faz 2B.8.1) senaryolar - "worker ZATEN değerlendirdi" durumunu simüle eder ----

    [Fact]
    public async Task KuyrukBosOlmasiTekBasinaUnhealthyUretmez()
    {
        // Faz 2B.8 görev md.15 - "kuyruk boş diye unhealthy olmamalı": döngü başladı, YAKIN
        // zamanda başarılı bir poll KAYDETTİ (mesaj bulunsun/bulunmasın FARK ETMEZ) → Healthy.
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, gate, options, timeProvider);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
    }

    [Fact]
    public async Task SonWorkerHatasiSonBasariliPolldanYeniyseDegradedOlurAmaUnhealthyDegil()
    {
        // Faz 2B.8.1 görev md.10 - "en yeni olayın hangisi olduğuna göre karar ver": hata, en son
        // BAŞARILI polldan DAHA YENİYSE (henüz bir sonraki tur BAŞARIYLA tamamlanmadıysa) - bu
        // GÖRÜNÜR olmalıdır (Degraded) - ama TEK bir hata, Unhealthy kadar CİDDİ SAYILMAZ.
        var timeProvider = new MutableTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        timeProvider.SetUtcNow(Baslangic.AddSeconds(1));
        healthState.RecordWorkerError("EBELGE_OUTBOX_WORKER_SQL_HATASI");

        var check = CreateCheck(healthState, gate, options, Baslangic.AddSeconds(1));
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
        Assert.Contains("lastWorkerErrorSafeCode", sonuc.Data.Keys);
        Assert.Equal("EBELGE_OUTBOX_WORKER_SQL_HATASI", sonuc.Data["lastWorkerErrorSafeCode"]);
    }

    [Fact]
    public async Task WorkerHatasindanSonraBasariliPollRecoveryUretir()
    {
        // Faz 2B.8.1 görev md.10, test senaryo 24 - hata SONRASINDA (daha YENİ bir zamanda)
        // başarılı bir poll GERÇEKLEŞİRSE, worker "toparlanmış" kabul edilir - eski hata KAYITTA
        // KALIR (temizlenmez) ama artık health kararını ETKİLEMEZ.
        var timeProvider = new MutableTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();

        healthState.RecordWorkerError("EBELGE_OUTBOX_WORKER_SQL_HATASI");

        timeProvider.SetUtcNow(Baslangic.AddSeconds(5));
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, gate, options, Baslangic.AddSeconds(5));
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
        // Eski hata KAYITTA kalır (silinmez) - yalnız health KARARINI artık ETKİLEMEZ.
        Assert.Equal("EBELGE_OUTBOX_WORKER_SQL_HATASI", sonuc.Data["lastWorkerErrorSafeCode"]);
    }

    [Fact]
    public async Task UzunSuredirIlerlemeyenDonguDegradedOlur()
    {
        var startTimeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, startTimeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(startTimeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll(); // Baslangic anında kaydedildi

        // 30sn idle aralığının 5 katı (Degraded eşiği) = 150sn; 2 saat AÇIKÇA bunu aşar AMA
        // 20 katı (Unhealthy eşiği, 600sn) İLE karşılaştırıldığında (2 saat = 7200sn) BUNU DA aşar
        // - bu yüzden BU test yalnız "Degraded EŞİĞİ" içinde kalan, kritik EŞİĞİN altında bir süre
        // kullanmalıdır (60 dakika = 3600sn > 600sn Unhealthy eşiği bile OLURDU - AŞAĞIDAKİ süre
        // KASITLI OLARAK Degraded ile Unhealthy arasında SEÇİLİR: 150sn < 300sn < 600sn).
        var araSure = startTimeProvider.GetUtcNow().AddSeconds(300);
        var check = CreateCheck(healthState, gate, options, araSure);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
    }

    [Fact]
    public async Task CokUzunSuredirIlerlemeyenDonguUnhealthyOlur()
    {
        var startTimeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, startTimeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(startTimeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        // Unhealthy eşiği = 30sn * 20 = 600sn - AÇIKÇA aşan bir süre.
        var cokSonra = startTimeProvider.GetUtcNow().AddSeconds(1200);
        var check = CreateCheck(healthState, gate, options, cokSonra);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, sonuc.Status);
    }

    [Fact]
    public async Task InflightSayisiHealthCikitisindaDogruGorunur()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();
        healthState.IncrementInflight();
        healthState.IncrementInflight();
        healthState.DecrementInflight();

        var check = CreateCheck(healthState, gate, options, timeProvider);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(1, sonuc.Data["inflight"]);
    }

    [Fact]
    public async Task HealthOutputPiiVeyaTokenIcermez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true);
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, gate, options, timeProvider);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        var serileştirilmis = string.Join(
            " ",
            sonuc.Data.Select(kv => $"{kv.Key}={kv.Value}").Append(sonuc.Description ?? string.Empty));

        Assert.DoesNotContain("KilitToken", serileştirilmis, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", serileştirilmis, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", serileştirilmis); // XML içeriği yok
        // Ham config değerleri (NotBeforeLocalDate/TimeZoneId) output'ta HİÇ YER ALMAZ - yalnız
        // type-safe `activationReason` enum adı bulunur.
        Assert.DoesNotContain("Europe/Istanbul", serileştirilmis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GecersizConfigHealthOutputundaHamDegerIcermezYalnizReasonIcerir()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var options = Options_(enabled: true, timeZoneId: "Gecersiz/TimeZone-Kimligi");
        var gate = CreateGate(options, timeProvider);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.InvalidTimeZoneConfiguration());

        var check = CreateCheck(healthState, gate, options, timeProvider);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        var serileştirilmis = string.Join(" ", sonuc.Data.Select(kv => $"{kv.Key}={kv.Value}"));
        Assert.DoesNotContain("Gecersiz/TimeZone", serileştirilmis, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration), sonuc.Data["activationReason"]);
    }
}
