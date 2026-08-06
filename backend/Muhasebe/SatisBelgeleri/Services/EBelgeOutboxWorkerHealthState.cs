namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Faz 2B.8 görev md.15 - `EBelgeOutboxWorkerHealthCheck`'in okuduğu, PII/token İÇERMEYEN anlık worker durumu.</summary>
public sealed record EBelgeOutboxWorkerHealthSnapshot(
    bool LoopStarted,
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
/// </summary>
public interface IEBelgeOutboxWorkerHealthState
{
    void RecordLoopStarted();

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
            return new EBelgeOutboxWorkerHealthSnapshot(
                _loopStarted,
                _lastSuccessfulPollUtc,
                _lastWorkerErrorUtc,
                _lastWorkerErrorSafeCode,
                Volatile.Read(ref _inflight));
        }
    }
}
