using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8 görev md.15 - hafif bir worker health check. Sidecar health check'ini TEKRAR ETMEZ
/// (çözümde başka bir bileşene özel `IHealthCheck` YOKTUR - bu, İLK örnektir). PII/token health
/// output'una EKLENMEZ - yalnız `EBelgeOutboxWorkerHealthSnapshot`'ın GÜVENLİ alanları raporlanır.
///
/// Kararlar (görev md.15'in açık istediği şekilde belgelenmiştir):
/// - Worker `Enabled=false` İSE → `Healthy` (KASITLI, beklenen bir devre-dışı durumdur - PAGE/alarm
///   ÜRETMEMELİDİR).
/// - Aktivasyon kapısı (tarih/config) KAPALI İSE (henüz 15 Eylül 2026 gelmemiş VEYA config
///   geçersiz/fail-closed) → YİNE `Healthy` - bu da BEKLENEN bir operasyonel durumdur, worker'ın
///   KENDİSİ bozuk DEĞİLDİR.
/// - Worker döngüsü HİÇ başlamamışsa (`Enabled=true` olmasına RAĞMEN) → `Unhealthy` (başlangıç
///   arızasına işaret eder).
/// - Döngü başladı AMA son başarılı poll ZAMAN AŞIMINA UĞRAMIŞSA (uzun süredir ilerlemiyor) →
///   `Degraded`.
/// - Kuyruk BOŞ olması (mesaj yokluğu) TEK BAŞINA HİÇBİR ZAMAN unhealthy/degraded ÜRETMEZ - yalnız
///   döngünün KENDİSİNİN ilerleyip ilerlemediği ölçülür, kuyruk DERİNLİĞİ DEĞİL.
/// - TEK bir mesajın terminal İŞ hatası (ör. XSD/Schematron başarısız) BURADA HİÇ GÖRÜNMEZ - health
///   state yalnız WORKER-SEVİYESİ (beklenmedik) hataları TUTAR (bkz. `EBelgeOutboxWorkerHealthState`).
/// </summary>
public sealed class EBelgeOutboxWorkerHealthCheck : IHealthCheck
{
    private readonly IEBelgeOutboxWorkerHealthState _healthState;
    private readonly EBelgeProcessingOptions _options;
    private readonly TimeProvider _timeProvider;

    public EBelgeOutboxWorkerHealthCheck(
        IEBelgeOutboxWorkerHealthState healthState,
        IOptions<EBelgeProcessingOptions> options,
        TimeProvider timeProvider)
    {
        _healthState = healthState;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("E-belge outbox worker devre dışı (config) - kasıtlı, beklenen bir durum."));
        }

        var snapshot = _healthState.GetSnapshot();
        var data = new Dictionary<string, object>
        {
            ["inflight"] = snapshot.InflightCount,
            ["lastSuccessfulPollUtc"] = snapshot.LastSuccessfulPollUtc?.ToString("O") ?? "-",
            ["lastWorkerErrorUtc"] = snapshot.LastWorkerErrorUtc?.ToString("O") ?? "-",
            ["lastWorkerErrorSafeCode"] = snapshot.LastWorkerErrorSafeCode ?? "-",
        };

        if (!snapshot.LoopStarted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("E-belge outbox worker döngüsü henüz başlamadı.", data: data));
        }

        var now = _timeProvider.GetUtcNow();
        var stalenessEsigi = TimeSpan.FromSeconds(Math.Max(_options.PollIntervalSeconds, _options.IdlePollIntervalSeconds) * 5);
        if (snapshot.LastSuccessfulPollUtc is null || now - snapshot.LastSuccessfulPollUtc.Value > stalenessEsigi)
        {
            return Task.FromResult(HealthCheckResult.Degraded("E-belge outbox worker döngüsü uzun süredir ilerlemiyor.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("E-belge outbox worker çalışıyor.", data));
    }
}
