namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8/2B.8.1/2B.8.2 görev md.9 - `EBelgeOutboxWorkerHealthCheck`'in okuduğu, PII/token
/// İÇERMEYEN anlık worker durumu. `ActivationReason`/`ActivationAllowed`/`WorkerEnabled`, ya
/// worker'ın HER polling turunda `IEBelgeProcessingActivationGate.Evaluate()`'DEN elde ettiği
/// kararı YA DA - worker döngüsü HENÜZ hiç değerlendirme YAPMAMIŞSA - health check'in AYNI gate
/// üzerinden yaptığı fallback değerlendirmeyi yansıtır (bkz. `EBelgeOutboxWorkerHealthCheck`, görev
/// md.2 - "farklı bir aktivasyon algoritması YAZILMAZ").
///
/// Faz 2B.8.2 görev md.1 - `ActivationEvaluated=false` İKEN `ActivationReason` HENÜZ `null`dur -
/// bu, GERÇEK bir `Disabled` kararıyla ASLA KARIŞTIRILMAZ (eski `?? Disabled()` varsayımı
/// KALDIRILDI). `WorkerEnabled`/`ActivationAllowed`, henüz değerlendirme YOKKEN GÜVENLİ/tutucu
/// `false` değerini TAŞIR - AMA bu durumda health check ZATEN `ActivationReason` yerine KENDİ
/// TAZE `Evaluate()` sonucunu KULLANIR (BURADAKİ `false` değerleri yalnız DOĞRUDAN
/// `GetSnapshot()` çağıran BAŞKA kod İÇİNDİR).
/// </summary>
public sealed record EBelgeOutboxWorkerHealthSnapshot(
    bool ActivationEvaluated,
    bool WorkerEnabled,
    bool ActivationAllowed,
    EBelgeProcessingActivationReason? ActivationReason,
    bool LoopStarted,
    DateTimeOffset? LoopStartedUtc,
    DateTimeOffset? LastSuccessfulPollUtc,
    DateTimeOffset? LastWorkerErrorUtc,
    string? LastWorkerErrorSafeCode,
    int InflightCount);

/// <summary>
/// Faz 2B.8 görev md.15 - worker'ın kendi thread-safe, PII/token İÇERMEYEN durum kaydı. Yalnız
/// WORKER-SEVİYESİ (polling turu/tek mesaj işleme sırasındaki beklenmedik) olayları TUTAR - TEK
/// bir mesajın normal bir TERMİNAL İŞ hatası (ör. XSD doğrulaması başarısız) burada HİÇ
/// KAYDEDİLMEZ (bkz. görev md.15, "tek bir mesajın terminal iş hatası nedeniyle unhealthy
/// olmamalı").
///
/// Faz 2B.8.1 görev md.10 - `LastWorkerErrorUtc`/`LastWorkerErrorSafeCode`, BAŞARILI bir poll
/// SONRASINDA TEMİZLENMEZ (BİLİNÇLİ karar) - hem "son hata" hem "son başarılı poll" zaman
/// damgaları KALICI olarak saklanır; health check, HANGİSİNİN daha YENİ olduğuna bakarak
/// toparlanma/tekrarlayan-hata ayrımını KENDİSİ yapar (bkz. `EBelgeOutboxWorkerHealthCheck`).
/// </summary>
public interface IEBelgeOutboxWorkerHealthState
{
    void RecordLoopStarted();

    /// <summary>Bkz. Faz 2B.8.1 görev md.7/md.9 - worker'ın HER turda değerlendirdiği aktivasyon kararını health state'e YANSITIR.</summary>
    void RecordActivationDecision(EBelgeProcessingActivationDecision decision);

    /// <summary>Bir polling turunun (mesaj bulunsun/bulunmasın) worker-seviyesinde bir exception FIRLATMADAN tamamlandığını kaydeder - "worker döngüsü CANLI/ilerliyor" anlamına gelir.</summary>
    void RecordSuccessfulPoll();

    /// <summary>Yalnız GÜVENLİ (PII/token içermeyen) bir hata KODU kaydedilir - hata mesajı/exception detayı ASLA saklanmaz.</summary>
    void RecordWorkerError(string safeErrorCode);

    void IncrementInflight();

    void DecrementInflight();

    EBelgeOutboxWorkerHealthSnapshot GetSnapshot();
}

public sealed class EBelgeOutboxWorkerHealthState : IEBelgeOutboxWorkerHealthState
{
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private bool _loopStarted;
    private DateTimeOffset? _loopStartedUtc;
    private EBelgeProcessingActivationDecision? _sonAktivasyonKarari;
    private DateTimeOffset? _lastSuccessfulPollUtc;
    private DateTimeOffset? _lastWorkerErrorUtc;
    private string? _lastWorkerErrorSafeCode;
    private int _inflight;

    public EBelgeOutboxWorkerHealthState(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void RecordLoopStarted()
    {
        lock (_lock)
        {
            _loopStarted = true;
            _loopStartedUtc = _timeProvider.GetUtcNow();
        }
    }

    public void RecordActivationDecision(EBelgeProcessingActivationDecision decision)
    {
        lock (_lock)
        {
            _sonAktivasyonKarari = decision;
        }
    }

    public void RecordSuccessfulPoll()
    {
        lock (_lock)
        {
            _lastSuccessfulPollUtc = _timeProvider.GetUtcNow();
        }
    }

    public void RecordWorkerError(string safeErrorCode)
    {
        lock (_lock)
        {
            _lastWorkerErrorUtc = _timeProvider.GetUtcNow();
            _lastWorkerErrorSafeCode = safeErrorCode;
        }
    }

    public void IncrementInflight() => Interlocked.Increment(ref _inflight);

    public void DecrementInflight() => Interlocked.Decrement(ref _inflight);

    public EBelgeOutboxWorkerHealthSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            // Faz 2B.8.2 görev md.1 - `RecordActivationDecision` HENÜZ hiç çağrılmadıysa,
            // "henüz değerlendirilmedi" GERÇEK bir `Disabled` kararıyla KARIŞTIRILMAZ - `null`
            // olarak AÇIKÇA taşınır. Bu durumu YORUMLAMAK (fallback değerlendirme YAPMAK)
            // `EBelgeOutboxWorkerHealthCheck`'in sorumluluğudur - health state'in KENDİSİ hiçbir
            // VARSAYIM yapmaz.
            var karar = _sonAktivasyonKarari;

            return new EBelgeOutboxWorkerHealthSnapshot(
                ActivationEvaluated: karar is not null,
                WorkerEnabled: karar is not null && karar.Reason != EBelgeProcessingActivationReason.Disabled,
                ActivationAllowed: karar?.CanProcess ?? false,
                ActivationReason: karar?.Reason,
                LoopStarted: _loopStarted,
                LoopStartedUtc: _loopStartedUtc,
                LastSuccessfulPollUtc: _lastSuccessfulPollUtc,
                LastWorkerErrorUtc: _lastWorkerErrorUtc,
                LastWorkerErrorSafeCode: _lastWorkerErrorSafeCode,
                InflightCount: Volatile.Read(ref _inflight));
        }
    }
}
