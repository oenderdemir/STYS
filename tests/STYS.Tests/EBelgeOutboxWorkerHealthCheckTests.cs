using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>Faz 2B.8 görev md.15/md.18 - worker health check'in disabled/gate-kapalı/stale-loop/normal durumlarında ürettiği kararları doğrular. PII/token İÇERMEZ.</summary>
public class EBelgeOutboxWorkerHealthCheckTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
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
    public async Task WorkerDisabledIkenHealthy()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
        var check = CreateCheck(healthState, Options_(enabled: false), Baslangic);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
    }

    [Fact]
    public async Task DonguHicBaslamadiysaUnhealthy()
    {
        var healthState = new EBelgeOutboxWorkerHealthState(new FixedTimeProvider(Baslangic));
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
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
    }

    [Fact]
    public async Task TekBirWorkerHatasiUnhealthyUretmez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll();
        healthState.RecordWorkerError("EBELGE_OUTBOX_WORKER_SQL_HATASI");

        var check = CreateCheck(healthState, Options_(enabled: true), Baslangic);
        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, sonuc.Status);
        Assert.Contains("lastWorkerErrorSafeCode", sonuc.Data.Keys);
        Assert.Equal("EBELGE_OUTBOX_WORKER_SQL_HATASI", sonuc.Data["lastWorkerErrorSafeCode"]);
    }

    [Fact]
    public async Task UzunSuredirIlerlemeyenDonguDegradedOlur()
    {
        var startTimeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(startTimeProvider);
        healthState.RecordLoopStarted();
        healthState.RecordSuccessfulPoll(); // Baslangic anında kaydedildi

        var cokSonra = Baslangic.AddHours(2);
        var check = CreateCheck(healthState, Options_(enabled: true), cokSonra);

        var sonuc = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, sonuc.Status);
    }

    [Fact]
    public async Task HealthOutputPiiVeyaTokenIcermez()
    {
        var timeProvider = new FixedTimeProvider(Baslangic);
        var healthState = new EBelgeOutboxWorkerHealthState(timeProvider);
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
    }
}
