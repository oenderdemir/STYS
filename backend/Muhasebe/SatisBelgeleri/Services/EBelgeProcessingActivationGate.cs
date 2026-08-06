using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Faz 2B.8.1 görev md.7 - `EBelgeProcessingActivationGate.Evaluate()`'in type-safe sonucu için ayrım kümesi.</summary>
public enum EBelgeProcessingActivationReason
{
    /// <summary>`Enabled=true`, config geçerli VE tarih kapısı geçilmiş - worker mesaj claim EDEBİLİR.</summary>
    Active = 1,

    /// <summary>`Enabled=false` - KASITLI, beklenen bir devre-dışı durum.</summary>
    Disabled = 2,

    /// <summary>`Enabled=true`, config geçerli AMA `TimeProvider`'ın şu anki UTC zamanı henüz `NotBeforeLocalDate`'e ULAŞMAMIŞ.</summary>
    BeforeActivationDate = 3,

    /// <summary>`NotBeforeLocalDate` "yyyy-MM-dd" formatında GEÇERLİ bir tarih DEĞİL - fail-closed.</summary>
    InvalidDateConfiguration = 4,

    /// <summary>`TimeZoneId`, `TimeZoneInfo.FindSystemTimeZoneById` tarafından TANINMIYOR - fail-closed.</summary>
    InvalidTimeZoneConfiguration = 5,
}

/// <summary>Faz 2B.8.1 görev md.7 - `bool ShouldProcess()`'in YERİNE geçen, type-safe aktivasyon kararı. Worker VE health check AYNI kararı (aynı `Evaluate()` çağrısından) kullanır.</summary>
public sealed record EBelgeProcessingActivationDecision(bool CanProcess, EBelgeProcessingActivationReason Reason)
{
    public static EBelgeProcessingActivationDecision Active() => new(true, EBelgeProcessingActivationReason.Active);

    public static EBelgeProcessingActivationDecision Disabled() => new(false, EBelgeProcessingActivationReason.Disabled);

    public static EBelgeProcessingActivationDecision BeforeActivationDate() => new(false, EBelgeProcessingActivationReason.BeforeActivationDate);

    public static EBelgeProcessingActivationDecision InvalidDateConfiguration() => new(false, EBelgeProcessingActivationReason.InvalidDateConfiguration);

    public static EBelgeProcessingActivationDecision InvalidTimeZoneConfiguration() => new(false, EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration);
}

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
    /// <summary>Faz 2B.8.1 görev md.7 - type-safe karar. Worker VE health check, AYNI değerlendirmeyi (worker'ın HER turda çağırdığı TEK `Evaluate()` çağrısını) paylaşır - health check KENDİSİ AYRICA `Evaluate()` ÇAĞIRMAZ, worker'ın health state'e YAZDIĞI SONUCU okur.</summary>
    EBelgeProcessingActivationDecision Evaluate();

    /// <summary>Geriye uyumluluk İÇİN korunur - `Evaluate().CanProcess` İLE AYNIDIR.</summary>
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
///
/// Faz 2B.8.1 görev md.8 - `Evaluate()` HER polling turunda (10-30sn aralıkla) çağrıldığından,
/// geçersiz config HER çağrıda `Error` loglarsa LOG SPAM oluşurdu. Bu sınıf, AYNI (neden, değer)
/// çifti İÇİN yalnız İLK KEZ (veya ÖNCEKİ değerden FARKLI bir değere geçişte) loglar - config
/// DÜZELİRSE iz TEMİZLENİR, gelecekte YENİDEN bozulursa TEKRAR loglanabilir.
/// </summary>
public sealed class EBelgeProcessingActivationGate : IEBelgeProcessingActivationGate
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly EBelgeProcessingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EBelgeProcessingActivationGate> _logger;

    private readonly object _logLock = new();
    private EBelgeProcessingActivationReason? _sonLoglananGecersizConfigNedeni;
    private string? _sonLoglananGecersizConfigDegeri;

    public EBelgeProcessingActivationGate(
        IOptions<EBelgeProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<EBelgeProcessingActivationGate> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool ShouldProcess() => Evaluate().CanProcess;

    public EBelgeProcessingActivationDecision Evaluate()
    {
        if (!_options.Enabled)
        {
            ResetGecersizConfigLogState();
            return EBelgeProcessingActivationDecision.Disabled();
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            LogGecersizConfigBirKezSekilde(EBelgeProcessingActivationReason.InvalidTimeZoneConfiguration, _options.TimeZoneId, ex);
            return EBelgeProcessingActivationDecision.InvalidTimeZoneConfiguration();
        }

        if (!DateOnly.TryParseExact(_options.NotBeforeLocalDate, DateFormat, out var notBeforeLocalDate))
        {
            LogGecersizConfigBirKezSekilde(EBelgeProcessingActivationReason.InvalidDateConfiguration, _options.NotBeforeLocalDate ?? "(null)", null);
            return EBelgeProcessingActivationDecision.InvalidDateConfiguration();
        }

        // Config GEÇERLİ - önceki geçersiz-config log izini TEMİZLE (görev md.8, "config
        // düzeldiğinde yeni durum yeniden değerlendirilebilmeli" - gelecekte YENİDEN bozulursa
        // TEKRAR loglanabilsin diye).
        ResetGecersizConfigLogState();

        var notBeforeLocalMidnightUnspecified = DateTime.SpecifyKind(notBeforeLocalDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var notBeforeUtc = TimeZoneInfo.ConvertTimeToUtc(notBeforeLocalMidnightUnspecified, zone);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return nowUtc >= notBeforeUtc
            ? EBelgeProcessingActivationDecision.Active()
            : EBelgeProcessingActivationDecision.BeforeActivationDate();
    }

    private void LogGecersizConfigBirKezSekilde(EBelgeProcessingActivationReason neden, string deger, Exception? ex)
    {
        lock (_logLock)
        {
            if (_sonLoglananGecersizConfigNedeni == neden && _sonLoglananGecersizConfigDegeri == deger)
            {
                return;
            }

            _sonLoglananGecersizConfigNedeni = neden;
            _sonLoglananGecersizConfigDegeri = deger;
        }

        // Faz 2B.8.1 görev md.5 İLE TUTARLI - exception nesnesinin KENDİSİ logger'a GEÇİRİLMEZ
        // (yalnız tip adı) - `TimeZoneNotFoundException`/tarih parse hataları PII TAŞIMAZ, ama
        // worker alt sistemindeki TÜM loglama AYNI güvenli disiplini izler.
        _logger.LogError(
            "EBelgeProcessing aktivasyon kapısı geçersiz config - fail-closed. Neden={Neden}, ExceptionType={ExceptionType}",
            neden, ex?.GetType().Name ?? "-");
    }

    private void ResetGecersizConfigLogState()
    {
        lock (_logLock)
        {
            _sonLoglananGecersizConfigNedeni = null;
            _sonLoglananGecersizConfigDegeri = null;
        }
    }
}
