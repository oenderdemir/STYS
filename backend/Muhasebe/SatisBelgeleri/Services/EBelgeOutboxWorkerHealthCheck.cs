using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8/2B.8.1 görev md.9 - hafif bir worker health check. Sidecar health check'ini TEKRAR
/// ETMEZ. PII/token/ham config değeri health output'una EKLENMEZ - yalnız
/// `EBelgeOutboxWorkerHealthSnapshot`'ın GÜVENLİ, type-safe alanları raporlanır.
///
/// Karar politikası (görev md.9'un AÇIKÇA istediği kategoriler):
///
/// **Healthy**: `ActivationReason=Disabled` (KASITLI devre dışı) VEYA `ActivationReason=
/// BeforeActivationDate` (beklenen tarih kapısı) VEYA (`Active` VE döngü ilerliyor VE son
/// başarılı poll GÜNCEL).
///
/// **Degraded**: `ActivationReason=InvalidDateConfiguration` VEYA `InvalidTimeZoneConfiguration`
/// (config hatası GÖRÜNÜR olmalı - artık sessizce Healthy SAYILMAZ) VEYA (`Active` VE döngü
/// başladı AMA son başarılı poll "Degraded eşiğini" aştı) VEYA (`Active` VE en son worker-seviyesi
/// hata, en son başarılı polldan DAHA YENİ - bkz. görev md.10, "toparlanma" mantığı).
///
/// **Unhealthy**: `Active` VE döngü HİÇ başlamamış (başlangıç arızası) VEYA `Active` VE son
/// başarılı poll "Unhealthy (kritik) eşiğini" aştı.
///
/// Kuyruk BOŞ olması (mesaj yokluğu) TEK BAŞINA HİÇBİR ZAMAN unhealthy/degraded ÜRETMEZ - yalnız
/// döngünün KENDİSİNİN ilerleyip ilerlemediği ölçülür. TEK bir mesajın terminal İŞ hatası (ör.
/// XSD/Schematron başarısız) BURADA HİÇ GÖRÜNMEZ - health state yalnız WORKER-SEVİYESİ (beklenmedik)
/// hataları TUTAR.
/// </summary>
public sealed class EBelgeOutboxWorkerHealthCheck : IHealthCheck
{
    /// <summary>Son başarılı poll bu kadar (Poll/Idle aralığının EN BÜYÜĞÜ İLE çarpımı kadar) GECİKTİYSE Degraded.</summary>
    private const int DegradedStalenessCarpani = 5;

    /// <summary>Son başarılı poll bu kadar GECİKTİYSE (daha KRİTİK bir eşik) Unhealthy.</summary>
    private const int UnhealthyStalenessCarpani = 20;

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
        var snapshot = _healthState.GetSnapshot();
        var data = BuildGuvenliData(snapshot);

        switch (snapshot.ActivationReason)
        {
            case EBelgeProcessingActivationReason.Disabled:
                return Task.FromResult(HealthCheckResult.Healthy(
                    "E-belge outbox worker devre dışı (config) - kasıtlı, beklenen bir durum.", data));

            case EBelgeProcessingActivationReason.BeforeActivationDate:
                return Task.FromResult(HealthCheckResult.Healthy(
                    "E-belge outbox worker aktivasyon tarihi henüz gelmedi - beklenen bir durum.", data));

            case EBelgeProcessingActivationReason.InvalidDateConfiguration:
                return Task.FromResult(HealthCheckResult.Degraded(
                    "E-belge outbox worker aktivasyon tarihi config'i geçersiz (fail-closed uygulanıyor).", data: data));

            case EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration:
                return Task.FromResult(HealthCheckResult.Degraded(
                    "E-belge outbox worker timezone config'i geçersiz (fail-closed uygulanıyor).", data: data));
        }

        // ActivationReason == Active.
        if (!snapshot.LoopStarted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "E-belge outbox worker aktivasyon aktif olmasına rağmen döngü henüz başlamadı.", data: data));
        }

        var now = _timeProvider.GetUtcNow();
        var temelAralikSaniye = Math.Max(_options.PollIntervalSeconds, _options.IdlePollIntervalSeconds);
        var degradedEsigi = TimeSpan.FromSeconds((double)temelAralikSaniye * DegradedStalenessCarpani);
        var unhealthyEsigi = TimeSpan.FromSeconds((double)temelAralikSaniye * UnhealthyStalenessCarpani);

        // HENÜZ hiçbir tur BAŞARIYLA tamamlanmadıysa (`LastSuccessfulPollUtc` null) - döngünün
        // BAŞLANGIÇ zamanını referans alır (başlangıçtan bu yana geçen süre HENÜZ eşiği
        // AŞMADIYSA bu, worker'ın YENİ başladığı, henüz İLK turunu bitirmediği KISA/normal bir
        // pencereyi - Unhealthy DEĞİL - temsil eder).
        var referansZaman = snapshot.LastSuccessfulPollUtc ?? snapshot.LoopStartedUtc ?? now;
        var pollBayatligi = now - referansZaman;

        if (pollBayatligi > unhealthyEsigi)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "E-belge outbox worker döngüsü kritik eşiği aşacak kadar uzun süredir ilerlemiyor.", data: data));
        }

        // Faz 2B.8.1 görev md.10 - "en yeni olayın hangisi olduğuna göre karar ver": son
        // worker-seviyesi hata, son başarılı polldan DAHA YENİYSE (aynı/sonraki turda BAŞARILI bir
        // poll HENÜZ kaydedilmediyse) - tekrarlayan bir soruna İŞARET edebilir, Degraded. Henüz
        // hiç başarılı poll YOKSA (`LastSuccessfulPollUtc` null) VE bir hata KAYITLIYSA, hata
        // KESİNLİKLE "daha yeni" sayılır (karşılaştıracak daha ÖNCEKİ bir başarı YOK).
        var sonHataDahaYeniMi = snapshot.LastWorkerErrorUtc is not null
            && (snapshot.LastSuccessfulPollUtc is null || snapshot.LastWorkerErrorUtc.Value > snapshot.LastSuccessfulPollUtc.Value);

        if (pollBayatligi > degradedEsigi || sonHataDahaYeniMi)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "E-belge outbox worker döngüsü yavaşladı veya yakın zamanda worker-seviyesi bir hata oluştu.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("E-belge outbox worker çalışıyor.", data));
    }

    /// <summary>Bkz. görev md.9 - "health output'una config'in ham geçersiz değeri veya PII ekleme; yalnız type-safe reason ekle." Ham `NotBeforeLocalDate`/`TimeZoneId` DEĞERLERİ BURADA HİÇ YER ALMAZ.</summary>
    private static Dictionary<string, object> BuildGuvenliData(EBelgeOutboxWorkerHealthSnapshot snapshot) => new()
    {
        ["workerEnabled"] = snapshot.WorkerEnabled,
        ["activationAllowed"] = snapshot.ActivationAllowed,
        ["activationReason"] = snapshot.ActivationReason.ToString(),
        ["loopStarted"] = snapshot.LoopStarted,
        ["inflight"] = snapshot.InflightCount,
        ["lastSuccessfulPollUtc"] = snapshot.LastSuccessfulPollUtc?.ToString("O") ?? "-",
        ["lastWorkerErrorUtc"] = snapshot.LastWorkerErrorUtc?.ToString("O") ?? "-",
        ["lastWorkerErrorSafeCode"] = snapshot.LastWorkerErrorSafeCode ?? "-",
    };
}
