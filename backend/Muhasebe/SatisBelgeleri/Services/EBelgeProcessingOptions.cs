using Microsoft.Extensions.Options;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// `EBelgeProcessing` config bölümü - Faz 2B.8 hosted outbox worker'ının GENEL üretim aktivasyon
/// kapısı VE zamanlama/paralellik ayarları (bkz. görev md.3). Bu, `EBelgeSigningOptions`'IN
/// (yalnız "imzalama mesajı OLUŞTURULSUN mu" sorusunu yanıtlayan, farklı bir gate) YERİNE
/// GEÇMEZ - AYRI, EK bir savunma katmanıdır: kuyrukta zaten var olan (yanlışlıkla veya elle
/// oluşturulmuş) mesajların worker TARAFINDAN CLAIM EDİLİP EDİLMEYECEĞİNİ kontrol eder. Varsayılan
/// (`appsettings.json`) değerleri `Enabled=false` olmalıdır - üretimde worker, bir operatörün
/// AÇIKÇA `true`'ya çevirmesi VE tarih kapısını geçmesi olmadan ASLA mesaj claim etmez.
/// </summary>
public sealed class EBelgeProcessingOptions
{
    public const string SectionName = "EBelgeProcessing";

    /// <summary>Üst sınır - `BatchSize` bunu aşarsa startup validation REDDEDER (bkz. görev md.10, "makul üst sınır").</summary>
    public const int MaxBatchSize = 500;

    /// <summary>Üst sınır - `MaxParallelism` bunu aşarsa startup validation REDDEDER (bkz. görev md.6, "bounded olmalı").</summary>
    public const int MaxParallelismLimit = 32;

    public bool Enabled { get; set; }

    /// <summary>"yyyy-MM-dd" formatında, `TimeZoneId` yerel takvim tarihi - bu tarihten ÖNCE (yerel gün başlangıcından önce) worker HİÇBİR mesaj CLAIM ETMEZ.</summary>
    public string? NotBeforeLocalDate { get; set; }

    /// <summary>Sunucunun local timezone'una GÜVENİLMEZ (bkz. görev md.3) - IANA/Windows timezone kimliği, varsayılan Europe/Istanbul.</summary>
    public string TimeZoneId { get; set; } = "Europe/Istanbul";

    /// <summary>Bir mesaj işlendikten SONRA bir sonraki claim denemesinden önce beklenecek süre.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Kuyrukta HİÇ mesaj bulunamadığında (VEYA aktivasyon kapısı kapalıyken) beklenecek, daha UZUN süre.</summary>
    public int IdlePollIntervalSeconds { get; set; } = 30;

    /// <summary>Bir polling turunda EN FAZLA kaç mesajın claim edileceği (bkz. görev md.4, "batch büyüklüğü kadar bounded claim çağrısı").</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Her claim için kullanılacak lease süresi - beklenen maksimum mesaj işleme süresinden GÜVENLİ biçimde UZUN tutulmalıdır (bkz. görev md.11, Seçenek A).</summary>
    public int LeaseDurationSeconds { get; set; } = 120;

    /// <summary>Aynı anda işlenebilecek EN FAZLA mesaj sayısı - üretim varsayılanı 1 (bkz. görev md.6).</summary>
    public int MaxParallelism { get; set; } = 1;

    /// <summary>Graceful shutdown sırasında çalışan mesajlara tanınacak EN FAZLA ek süre.</summary>
    public int ShutdownGracePeriodSeconds { get; set; } = 30;
}

/// <summary>
/// Faz 2B.8 görev md.10 - "Geçersiz seçenekler uygulama başlangıcında validation ile tespit
/// edilmeli". Bu, YALNIZ SAF ARİTMETİK/yapısal alan kontrolleridir (I/O YOK, dış bağımlılık YOK) -
/// bu yüzden `Enabled=false` OLSA BİLE KOŞULSUZ çalıştırılır (bir operatör gelecekte
/// `Enabled=true`'ya çevirdiğinde config'in ZATEN geçerli olduğundan emin olmak için). Tarih/
/// timezone doğrulaması BİLİNÇLİ OLARAK burada YAPILMAZ - bkz. <see cref="EBelgeProcessingActivationGate"/>
/// XML doc'u, "neden startup'ta DEĞİL, runtime'da fail-closed" gerekçesi için.
/// </summary>
public sealed class EBelgeProcessingOptionsValidator : IValidateOptions<EBelgeProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, EBelgeProcessingOptions options)
    {
        var hatalar = new List<string>();

        if (options.PollIntervalSeconds < 1)
        {
            hatalar.Add("EBelgeProcessing.PollIntervalSeconds en az 1 olmalıdır.");
        }

        if (options.IdlePollIntervalSeconds < options.PollIntervalSeconds)
        {
            hatalar.Add("EBelgeProcessing.IdlePollIntervalSeconds, PollIntervalSeconds değerinden küçük olamaz.");
        }

        if (options.BatchSize < 1 || options.BatchSize > EBelgeProcessingOptions.MaxBatchSize)
        {
            hatalar.Add($"EBelgeProcessing.BatchSize 1 ile {EBelgeProcessingOptions.MaxBatchSize} arasında olmalıdır.");
        }

        if (options.LeaseDurationSeconds < 1)
        {
            hatalar.Add("EBelgeProcessing.LeaseDurationSeconds pozitif olmalıdır.");
        }

        if (options.MaxParallelism < 1 || options.MaxParallelism > EBelgeProcessingOptions.MaxParallelismLimit)
        {
            hatalar.Add($"EBelgeProcessing.MaxParallelism 1 ile {EBelgeProcessingOptions.MaxParallelismLimit} arasında olmalıdır.");
        }

        if (options.ShutdownGracePeriodSeconds < 0)
        {
            hatalar.Add("EBelgeProcessing.ShutdownGracePeriodSeconds negatif olamaz.");
        }

        return hatalar.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(hatalar);
    }
}
