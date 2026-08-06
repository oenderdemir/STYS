using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8/2B.8.1 görev md.9/md.12 - worker health check'in aktivasyon-reason-tabanlı
/// Healthy/Degraded/Unhealthy politikasını doğrular. PII/token/ham config değeri İÇERMEZ.
/// `IEBelgeOutboxWorkerHealthState.RecordActivationDecision`, GERÇEK worker'ın HER polling
/// turunda çağırdığı ile AYNI - bu yüzden testler AÇIKÇA bu metodu çağırarak worker'ın davranışını
/// simüle eder (bkz. görev md.7, "worker ve health check aynı değerlendirme sonucunu kullanmalı").
/// </summary>
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

    private static readonly DateTimeOffset Baslangic = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static EBelgeOutboxWorkerHealthCheck CreateCheck(
        IEBelgeOutboxWorkerHealthState healthState, EBelgeProcessingOptions options, DateTimeOffset now)
        => new(healthState, Options.Create(options), new FixedTimeProvider(now));

    private static EBelgeProcessingOptions Options_(bool enabled = true) => new()
    {
        Enabled = enabled,
        PollIntervalSeconds = 10,
        IdlePollIntervalSeconds = 30,
    };

    [Fact]
    public async Task EnabledFalseIkenHealthyVeReasonDisabledGorunur()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Disabled());

        var check = CreateCheck(healthState, Options_(enabled: false), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.Disabled), sonuc.Data["activationReason"]);
        Assert.Equal(false, sonuc.Data["workerEnabled"]);
    }

    [Fact]
    public async Task TarihOncesiHealthyVeReasonBeforeActivationDateGorunur()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.BeforeActivationDate());

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.BeforeActivationDate), sonuc.Data["activationReason"]);
        Assert.Equal(true, sonuc.Data["workerEnabled"]);
    }

    [Fact]
    public async Task GecersizTimeZoneDegradedOlur()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.InvalidTimeZoneConfiguration());

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration), sonuc.Data["activationReason"]);
    }

    [Fact]
    public async Task GecersizTarihDegradedOlur()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.InvalidDateConfiguration());

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.InvalidDateConfiguration), sonuc.Data["activationReason"]);
    }

    [Fact]
    public async Task GateAcikFakatDonguHicBaslamadiysaUnhealthy()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        // RecordLoopStarted() KASITLI OLARAK çağrılmadı - başlangıç arızasını simüle eder.

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, sonuc.Status);
    }

    [Fact]
    public async Task KuyrukBosOlmasiTekBasinaUnhealthyUretmez()
    {
        // Faz 2B.8 görev md.15 - "kuyruk boş diye unhealthy olmamalı": döngü başladı, YAKIN
        // zamanda başarılı bir poll KAYDETTİ (mesaj bulunsun/bulunmasın FARK ETMEZ) → Healthy.
        var timeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
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
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        timeProvider.SetUtcNow(Baslangic.AddSeconds(1));
        healthState.RecordWorkerError("EBELGE_OUTBOX_WORKER_SQL_HATASI");

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic.AddSeconds(1));
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
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();

        healthState.RecordWorkerError("EBELGE_OUTBOX_WORKER_SQL_HATASI");

        timeProvider.SetUtcNow(Baslangic.AddSeconds(5));
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic.AddSeconds(5));
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
        // Eski hata KAYITTA kalır (silinmez) - yalnız health KARARINI artık ETKİLEMEZ.
        Assert.Equal("EBELGE_OUTBOX_WORKER_SQL_HATASI", sonuc.Data["lastWorkerErrorSafeCode"]);
    }

    [Fact]
    public async Task UzunSuredirIlerlemeyenDonguDegradedOlur()
    {
        var startTimeProvider = new FixedTimeProvider(Baslangic);
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
        var check = CreateCheck(healthState, Options_(enabled: true), araSure);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
    }

    [Fact]
    public async Task CokUzunSuredirIlerlemeyenDonguUnhealthyOlur()
    {
        var startTimeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(startTimeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        // Unhealthy eşiği = 30sn * 20 = 600sn - AÇIKÇA aşan bir süre.
        var cokSonra = startTimeProvider.GetUtcNow().AddSeconds(1200);
        var check = CreateCheck(healthState, Options_(enabled: true), cokSonra);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, sonuc.Status);
    }

    [Fact]
    public async Task InflightSayisiHealthCikitisindaDogruGorunur()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();
        healthState.IncrementInflight();
        healthState.IncrementInflight();
        healthState.DecrementInflight();

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(1, sonuc.Data["inflight"]);
    }

    [Fact]
    public async Task HealthOutputPiiVeyaTokenIcermez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.Active());
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
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
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        healthState.RecordActivationDecision(EBelgeProcessingActivationDecision.InvalidTimeZoneConfiguration());

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        var serileştirilmis = string.Join(" ", sonuc.Data.Select(kv => $"{kv.Key}={kv.Value}"));
        Assert.DoesNotContain("Gecersiz/TimeZone", serileştirilmis, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration), sonuc.Data["activationReason"]);
    }
}
