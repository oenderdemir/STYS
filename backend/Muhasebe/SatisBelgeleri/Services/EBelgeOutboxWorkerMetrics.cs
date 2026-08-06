using System.Diagnostics.Metrics;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8 görev md.13 - `System.Diagnostics.Metrics` üzerinden e-belge outbox worker ölçümleri.
/// Bu, çözümde BU deseni kullanan İLK sınıftır (yeni bir NuGet paketi GEREKMEZ - `Meter`/`Counter`/
/// `Histogram`/`UpDownCounter`, .NET BCL'nin bir parçasıdır). Tag olarak YALNIZ düşük cardinality
/// değerler (iş türü, sonuç türü) kullanılır - Outbox ID/EBelgeKaydi ID/Kurum ID/correlation ID/
/// hata mesajı/hash/token ASLA tag OLMAZ (bkz. görev md.13, açık liste).
/// </summary>
public interface IEBelgeOutboxWorkerMetrics
{
    void RecordClaimed(EBelgeOutboxIsTuru isTuru);

    void RecordResult(EBelgeOutboxIsTuru isTuru, EBelgeOutboxIslemeSonucuTuru sonucTuru, TimeSpan sure);

    void RecordPollError();

    void IncrementInflight();

    void DecrementInflight();
}

public sealed class EBelgeOutboxWorkerMetrics : IEBelgeOutboxWorkerMetrics, IDisposable
{
    public const string MeterName = "STYS.EBelge.Outbox";

    private readonly Meter _meter;
    private readonly Counter<long> _claimed;
    private readonly Counter<long> _completed;
    private readonly Counter<long> _retryScheduled;
    private readonly Counter<long> _terminalError;
    private readonly Counter<long> _leaseLost;
    private readonly Histogram<double> _processingDurationMs;
    private readonly Counter<long> _pollErrors;
    private readonly UpDownCounter<long> _inflight;

    public EBelgeOutboxWorkerMetrics()
    {
        _meter = new Meter(MeterName);
        _claimed = _meter.CreateCounter<long>("stys_ebelge_outbox_claimed_total");
        _completed = _meter.CreateCounter<long>("stys_ebelge_outbox_completed_total");
        _retryScheduled = _meter.CreateCounter<long>("stys_ebelge_outbox_retry_scheduled_total");
        _terminalError = _meter.CreateCounter<long>("stys_ebelge_outbox_terminal_error_total");
        _leaseLost = _meter.CreateCounter<long>("stys_ebelge_outbox_lease_lost_total");
        _processingDurationMs = _meter.CreateHistogram<double>("stys_ebelge_outbox_processing_duration_ms");
        _pollErrors = _meter.CreateCounter<long>("stys_ebelge_outbox_poll_errors_total");
        _inflight = _meter.CreateUpDownCounter<long>("stys_ebelge_outbox_inflight");
    }

    public void RecordClaimed(EBelgeOutboxIsTuru isTuru) =>
        _claimed.Add(1, new KeyValuePair<string, object?>("is_turu", isTuru.ToString()));

    public void RecordResult(EBelgeOutboxIsTuru isTuru, EBelgeOutboxIslemeSonucuTuru sonucTuru, TimeSpan sure)
    {
        var isTuruTag = new KeyValuePair<string, object?>("is_turu", isTuru.ToString());
        var sonucTag = new KeyValuePair<string, object?>("sonuc_turu", sonucTuru.ToString());

        _processingDurationMs.Record(sure.TotalMilliseconds, isTuruTag, sonucTag);

        switch (sonucTuru)
        {
            case EBelgeOutboxIslemeSonucuTuru.Tamamlandi:
                _completed.Add(1, isTuruTag);
                break;
            case EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi:
                _retryScheduled.Add(1, isTuruTag);
                break;
            case EBelgeOutboxIslemeSonucuTuru.TerminalHata:
                _terminalError.Add(1, isTuruTag);
                break;
            case EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi:
                _leaseLost.Add(1, isTuruTag);
                break;
        }
    }

    public void RecordPollError() => _pollErrors.Add(1);

    public void IncrementInflight() => _inflight.Add(1);

    public void DecrementInflight() => _inflight.Add(-1);

    public void Dispose() => _meter.Dispose();
}
