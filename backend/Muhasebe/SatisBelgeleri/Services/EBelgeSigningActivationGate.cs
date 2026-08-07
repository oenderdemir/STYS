using Microsoft.Extensions.Logging;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// `EBelgeSigning` config bölümü (bkz. Faz 2B.7 görev md.18). Varsayılan (`appsettings.json`)
/// değerleri `Enabled=false` olmalıdır - üretimde imzalama, bir operatörün AÇIKÇA `true`'ya
/// çevirmesi VE tarih kapısını geçmesi olmadan ASLA etkinleşmez.
/// </summary>
public sealed class EBelgeSigningOptions
{
    public const string SectionName = "EBelgeSigning";

    public bool Enabled { get; set; }

    /// <summary>"yyyy-MM-dd" formatında, Europe/Istanbul yerel takvim tarihi - bu tarihten ÖNCE (yerel gün başlangıcından önce) imzalama mesajı OLUŞTURULMAZ.</summary>
    public string? NotBeforeLocalDate { get; set; }
}

public interface IEBelgeSigningActivationGate
{
    /// <summary>`true` yalnız `Enabled=true` VE `TimeProvider`'ın şu anki UTC zamanı, `NotBeforeLocalDate`'in Europe/Istanbul yerel gün başlangıcına karşılık gelen UTC anına ULAŞMIŞSA döner. Config geçersizse (tarih parse edilemiyor, timezone bulunamıyor) FAIL-CLOSED olarak `false` döner (bkz. görev md.18).</summary>
    bool ShouldCreateSigningMessage();

    /// <summary>
    /// Faz 2B.10.2 görev md.5 - `ShouldCreateSigningMessage` İLE TAMAMEN AYNI (Enabled+
    /// NotBeforeLocalDate+Europe/Istanbul+TimeProvider+fail-closed) algoritmayı, semantik olarak
    /// daha DOĞRU bir isimle sunar: "yeni bir imzalama mesajı OLUŞTURULSUN mu" (üretim ANI)
    /// DEĞİL, "ŞU AN imzalama/SignedReady COMMIT edilebilir mi" (tüketim/commit ANI) sorusu İÇİN.
    /// Algoritma İKİNCİ KEZ YAZILMAZ - ikisi de AYNI iç değerlendirmeyi kullanır.
    /// </summary>
    bool CanSignNow();
}

/// <summary>
/// Server local timezone'a GÜVENMEZ - `Europe/Istanbul` AÇIKÇA kullanılır (bkz. görev md.18).
/// `SigningTime`/tarih kararları için `DateTime.Now`/`DateTime.UtcNow` KULLANILMAZ, yalnız
/// enjekte edilen <see cref="TimeProvider"/> üzerinden - testte sabitlenebilir (bkz. görev md.9,
/// aynı kural burada da geçerlidir).
/// </summary>
public sealed class EBelgeSigningActivationGate : IEBelgeSigningActivationGate
{
    private const string IstanbulTimeZoneId = "Europe/Istanbul";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly EBelgeSigningOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EBelgeSigningActivationGate> _logger;

    public EBelgeSigningActivationGate(
        Microsoft.Extensions.Options.IOptions<EBelgeSigningOptions> options,
        TimeProvider timeProvider,
        ILogger<EBelgeSigningActivationGate> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool ShouldCreateSigningMessage() => Degerlendir();

    public bool CanSignNow() => Degerlendir();

    /// <summary>Faz 2B.10.2 görev md.5 - `ShouldCreateSigningMessage`/`CanSignNow`'ın PAYLAŞTIĞI TEK karar - Enabled/tarih/timezone algoritması BURADA, TEK yerde yaşar.</summary>
    private bool Degerlendir()
    {
        if (!_options.Enabled)
        {
            return false;
        }

        TimeZoneInfo istanbul;
        try
        {
            istanbul = TimeZoneInfo.FindSystemTimeZoneById(IstanbulTimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogError(ex, "EBelgeSigning aktivasyon kapısı: Europe/Istanbul saat dilimi bulunamadı - fail-closed.");
            return false;
        }

        if (!DateOnly.TryParseExact(_options.NotBeforeLocalDate, DateFormat, out var notBeforeLocalDate))
        {
            _logger.LogError("EBelgeSigning aktivasyon kapısı: NotBeforeLocalDate ('{Deger}') geçerli bir yyyy-MM-dd tarihi değil - fail-closed.", _options.NotBeforeLocalDate);
            return false;
        }

        var notBeforeLocalMidnightUnspecified = DateTime.SpecifyKind(notBeforeLocalDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var notBeforeUtc = TimeZoneInfo.ConvertTimeToUtc(notBeforeLocalMidnightUnspecified, istanbul);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return nowUtc >= notBeforeUtc;
    }
}
