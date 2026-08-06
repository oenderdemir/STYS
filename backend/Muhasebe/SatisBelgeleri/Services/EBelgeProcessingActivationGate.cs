using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8 görev md.3/md.17 - hosted outbox worker'ın GENEL üretim aktivasyon kapısı.
/// `IEBelgeSigningActivationGate` (Faz 2B.7'den) yalnız "bir UblImzala mesajı OLUŞTURULSUN mu"
/// sorusunu yanıtlar - bu gate İSE, kuyrukta ZATEN var olan (o gate'ten BAĞIMSIZ, yanlışlıkla
/// veya elle eklenmiş OLABİLECEK) `ArtefaktOlustur`/`UblImzala` mesajlarının worker TARAFINDAN
/// CLAIM EDİLİP EDİLMEYECEĞİNİ kontrol eden, EK bir savunma katmanıdır - iki gate KASITLI OLARAK
/// BAĞIMSIZ/AYRI tutulur, birbirini ÇAĞIRMAZ.
/// </summary>
public interface IEBelgeProcessingActivationGate
{
    /// <summary>`true` yalnız `Enabled=true` VE `TimeProvider`'ın şu anki UTC zamanı, `NotBeforeLocalDate`'in `TimeZoneId` yerel gün başlangıcına karşılık gelen UTC anına ULAŞMIŞSA döner. Config geçersizse (tarih parse edilemiyor, timezone bulunamıyor) FAIL-CLOSED olarak `false` döner - bu durum bir exception fırlatmaz, worker'ı ÇÖKERTMEZ (bkz. görev md.3).</summary>
    bool ShouldProcess();
}

/// <summary>
/// Server local timezone'a GÜVENMEZ - `TimeZoneId` (varsayılan Europe/Istanbul) AÇIKÇA config'ten
/// okunur. Tarih/timezone kararları için `DateTime.Now`/`DateTime.UtcNow` KULLANILMAZ, yalnız
/// enjekte edilen <see cref="TimeProvider"/> üzerinden - testte sabitlenebilir.
///
/// Tarih/timezone doğrulaması BİLİNÇLİ OLARAK <see cref="EBelgeProcessingOptionsValidator"/>'a
/// (startup-time `IValidateOptions`) DEĞİL, BURAYA - HER çağrıda çalışan, ASLA fırlatmayan bir
/// runtime kontrolüne - konulmuştur: görev md.3 AÇIKÇA "Geçersiz tarih veya timezone config'i
/// fail-closed olmalı ... Disabled veya tarih kapısı kapalıyken worker uygulamayı crash
/// ettirmemeli" der - bir timezone/tarih hatasının UYGULAMA BAŞLANGICINI ENGELLEMESİ bu
/// gereksinimle DOĞRUDAN ÇELİŞİRDİ. Yapısal/sayısal alanlar (poll/idle/batch/lease/parallelism/
/// shutdown-grace) İSE - dış bağımlılığı OLMAYAN saf aritmetik kontroller olduğundan - GÜVENLE
/// startup'ta fail-fast edilebilir (bkz. `EBelgeProcessingOptionsValidator`).
/// </summary>
public sealed class EBelgeProcessingActivationGate : IEBelgeProcessingActivationGate
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly EBelgeProcessingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EBelgeProcessingActivationGate> _logger;

    public EBelgeProcessingActivationGate(
        IOptions<EBelgeProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<EBelgeProcessingActivationGate> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool ShouldProcess()
    {
        if (!_options.Enabled)
        {
            return false;
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogError(ex, "EBelgeProcessing aktivasyon kapısı: '{TimeZoneId}' saat dilimi bulunamadı - fail-closed.", _options.TimeZoneId);
            return false;
        }

        if (!DateOnly.TryParseExact(_options.NotBeforeLocalDate, DateFormat, out var notBeforeLocalDate))
        {
            _logger.LogError("EBelgeProcessing aktivasyon kapısı: NotBeforeLocalDate ('{Deger}') geçerli bir yyyy-MM-dd tarihi değil - fail-closed.", _options.NotBeforeLocalDate);
            return false;
        }

        var notBeforeLocalMidnightUnspecified = DateTime.SpecifyKind(notBeforeLocalDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var notBeforeUtc = TimeZoneInfo.ConvertTimeToUtc(notBeforeLocalMidnightUnspecified, zone);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return nowUtc >= notBeforeUtc;
    }
}
