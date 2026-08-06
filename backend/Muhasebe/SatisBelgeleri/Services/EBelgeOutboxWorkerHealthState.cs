namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8/2B.8.1 görev md.9 - `EBelgeOutboxWorkerHealthCheck`'in okuduğu, PII/token İÇERMEYEN
/// anlık worker durumu. `ActivationReason`/`ActivationAllowed`/`WorkerEnabled`, worker'ın HER
/// polling turunda `IEBelgeProcessingActivationGate.Evaluate()`'DEN elde ettiği AYNI kararı
/// yansıtır - health check KENDİSİ AYRICA gate'i DEĞERLENDİRMEZ (bkz. görev md.7, "worker ve
/// health check aynı değerlendirme sonucunu kullanmalı").
/// </summary>
public sealed record EBelgeOutboxWorkerHealthSnapshot(
    bool WorkerEnabled,
    bool ActivationAllowed,
    EBelgeProcessingActivationReason ActivationReason,
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
            // Faz 2B.8.1 - `RecordActivationDecision` HENÜZ hiç çağrılmadıysa (döngü henüz İLK
            // turunu TAMAMLAMADI) - GÜVENLİ/tutucu bir varsayılan: "devre dışı" GİBİ davran.
            // `LoopStarted` AYRICA izlendiğinden, bu geçici pencere health check'in Unhealthy
            // kararını YANLIŞ ETKİLEMEZ (bkz. EBelgeOutboxWorkerHealthCheck - Unhealthy yalnız
            // ActivationReason=Active VE LoopStarted=false iken tetiklenir; henüz DEĞERLENDİRME
            // YOKKEN ActivationReason bu varsayılanla Disabled görünür, Active DEĞİL).
            var karar = _sonAktivasyonKarari ?? EBelgeProcessingActivationDecision.Disabled();

            return new EBelgeOutboxWorkerHealthSnapshot(
                WorkerEnabled: karar.Reason != EBelgeProcessingActivationReason.Disabled,
                ActivationAllowed: karar.CanProcess,
                ActivationReason: karar.Reason,
                LoopStarted: _loopStarted,
                LoopStartedUtc: _loopStartedUtc,
                LastSuccessfulPollUtc: _lastSuccessfulPollUtc,
                LastWorkerErrorUtc: _lastWorkerErrorUtc,
                LastWorkerErrorSafeCode: _lastWorkerErrorSafeCode,
                InflightCount: Volatile.Read(ref _inflight));
        }
    }
}
