namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8 görev md.8 - `Task.Delay` yerine kullanılan, TEST EDİLEBİLİR bir zamanlama
/// abstraction'ı. Worker'ın idle/poll bekleme davranışını (hangi süre seçildi, cancellation
/// bekleme sırasında ANINDA kesiyor mu) gerçek zaman geçmeden doğrulayabilmek için - production
/// implementasyonu (<see cref="TimeProviderEBelgeOutboxWorkerDelay"/>) enjekte edilen
/// <see cref="TimeProvider"/> üzerinden çalışır (bkz. görev md.9, "SigningTime için AYNI kural").
/// </summary>
public interface IEBelgeOutboxWorkerDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>`TimeProvider`'ın kendi zamanlayıcı mekanizması (`CreateTimer`) üzerinden çalışan, `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` overload'unu SARAN GERÇEK implementasyon.</summary>
public sealed class TimeProviderEBelgeOutboxWorkerDelay : IEBelgeOutboxWorkerDelay
{
    private readonly TimeProvider _timeProvider;

    public TimeProviderEBelgeOutboxWorkerDelay(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, _timeProvider, cancellationToken);
}
